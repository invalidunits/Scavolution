using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RWCustom;
using UnityEngine;

namespace Scavolution
{
    
    // good ole reliable hack
    public static class ImperialMovementConnection
    {
        public static readonly MovementConnection.MovementType EnterPendulumZipline = (MovementConnection.MovementType)"enterPendulumZipline".GetHashCode();
        public static readonly MovementConnection.MovementType PendulumZipline = (MovementConnection.MovementType)"PendulumZipline".GetHashCode();
    }

    partial class ScavolutionPlugin
    {
        public void ImperialPathfindingHooks()
        {
            On.DebugMouse.Update += DebugMouse_Update;
            On.AImapper.FindPassagesOfCurrentTile += AIMapper_FindPassagesOfCurrentTile;
            On.CreatureTemplate.ConnectionResistance += (On.CreatureTemplate.orig_ConnectionResistance orig, CreatureTemplate self, MovementConnection.MovementType type) =>
            {
                if (type == ImperialMovementConnection.EnterPendulumZipline || type == ImperialMovementConnection.PendulumZipline)
                {
                    return new PathCost(0f, PathCost.Legality.IllegalConnection); // disable for now.
                }

                return orig(self, type);
            };


            On.RoomCamera.ChangeRoom += (On.RoomCamera.orig_ChangeRoom orig, RoomCamera self, Room newRoom, int cameraPosition) =>
            {
                orig(self, newRoom, cameraPosition);

                if (self.game.devToolsActive && self.game.mapVisible)
                {
                    newRoom.AddObject(new PendulumZiplineVisualizer());
                }
            };
        }

        public void AIMapper_FindPassagesOfCurrentTile(On.AImapper.orig_FindPassagesOfCurrentTile orig, AImapper self)
        {
            orig(self);

            try
            {
                var currentPos = new IntVector2(self.x, self.y);
                if (IsPendulumZiplineTile(self.map.map[currentPos.x, currentPos.y], currentPos, self.room))
                {
                    Logger.LogDebug($"Found pendulum tile, ({currentPos.x}, {currentPos.y})");

                    int foundneighbors = 0;
                    foreach (IntVector2 neighbor in Custom.eightDirections.Select(x => x + currentPos))
                    {
                        if (IsPendulumZiplineTile(self.map.map[neighbor.x, neighbor.y], neighbor, self.room))
                        {
                            ++foundneighbors;
                            var newConn = new MovementConnection(
                                ImperialMovementConnection.PendulumZipline, self.WrldCrd(currentPos), self.WrldCrd(neighbor), 1
                            );

                            self.map.map[currentPos.x, currentPos.y].outgoingPaths.Add(newConn);
                            self.map.map[neighbor.x, neighbor.y].incomingPaths.Add(newConn);
                        }
                    }

                    RoomExtensions.map.GetValue(self.room, x => new RoomExtensions(x)).ziplineTiles.Add(currentPos);
                    Logger.LogDebug($"Found {foundneighbors} neighbors ]:()");
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public class RoomExtensions
        {
            public static ConditionalWeakTable<Room, RoomExtensions> map = new();
            public Room room;
            public HashSet<IntVector2> ziplineTiles = new();
            public RoomExtensions(Room room)
            {
                this.room = room;
            }

        }

        public class AITileExtensions
        {
            public static ConditionalWeakTable<AItile, AITileExtensions> map = new();
            public AItile Tile { get; private set; }
            public int ceilingdistance = -1;
            public bool? zipline = null;
            public AITileExtensions(AItile tile)
            {
                Tile = tile;
            }

        }



        public bool IsPendulumZiplineTile(AItile tile, IntVector2 pos, Room room)
        {
            ref bool? zipline = ref AITileExtensions.map.GetValue(tile, x => new AITileExtensions(x)).zipline;
            if (!zipline.HasValue) zipline = BakeIsPendulumZiplineTile(tile, pos, room);
            return zipline.Value;
        }

        const int IDEAL_CEILING_DISTANCE = 7;
        const int MIN_CEILING_DISTANCE = 4;
        const int MIN_FLOOR_DISTANCE = 5;

        public bool BakeIsPendulumZiplineTile(AItile tile, IntVector2 pos, Room room)
        {
            if (tile.narrowSpace) return false;
            var flooraltitude = Mathf.Max(tile.floorAltitude, tile.smoothedFloorAltitude);

            if (flooraltitude < MIN_FLOOR_DISTANCE) return false;
            if (room.terrain != null && room.terrain.ObstructsTile(pos.x, pos.y)) return false;
            if (room.GetTile(pos.x, pos.y).Terrain == Room.Tile.TerrainType.Solid) return false;
            ref int ceilingdistance = ref AITileExtensions.map.GetValue(tile, x => new AITileExtensions(x)).ceilingdistance;
            if (ceilingdistance < 0)
            {
                ceilingdistance = 0;
                for (int i = pos.y; i < room.Height && room.GetTile(pos.x, i).Terrain != Room.Tile.TerrainType.Solid; i++)
                {
                    ceilingdistance = i - pos.y;
                }
            }

            if (ceilingdistance < MIN_CEILING_DISTANCE) return false;
            if (ceilingdistance > IDEAL_CEILING_DISTANCE) return false;
            else if (ceilingdistance != IDEAL_CEILING_DISTANCE && tile.smoothedFloorAltitude != MIN_FLOOR_DISTANCE) return false;
            return true;
        }


        private void DebugMouse_Update(On.DebugMouse.orig_Update orig, DebugMouse self, bool eu)
        {
            orig(self, eu);
            if (!self.room.readyForAI || !self.room.BeingViewed) return;

            string text = self.label.text;
            AItile aiTile = self.room.aimap.getAItile(self.pos);
            ref int ceilingdistance = ref AITileExtensions.map.GetValue(aiTile, x => new AITileExtensions(x)).ceilingdistance;

            text += $"\n\n--aiTile--" +
                $"tile: {self.room.GetTilePosition(self.pos)}\n" +
                $"acc: {aiTile.acc}\n" +
                $"floorAltitude: {aiTile.floorAltitude}    smoothed: {aiTile.smoothedFloorAltitude}\n" +
                $"ceilingDistance: {ceilingdistance}\n" +
                $"zipline: {IsPendulumZiplineTile(aiTile, self.room.GetTilePosition(self.pos), self.room)}";

            self.label.text = text;
            self.label2.text = text;
        }

        class PendulumZiplineVisualizer : CosmeticSprite
        {
            public PendulumZiplineVisualizer() : base() { }

            HashSet<MovementConnection> connections = new();

            public bool initialized = false;
            public override void Update(bool eu)
            {
                base.Update(eu);
                if (!room.BeingViewed) Destroy();
            }

            public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
            {
                base.InitiateSprites(sLeaser, rCam);
                foreach (IntVector2 tile in RoomExtensions.map.GetValue(room, x => new RoomExtensions(x)).ziplineTiles)
                {
                    AItile aiTile = room.aimap.getAItile(tile);
                    foreach (MovementConnection conn in aiTile.outgoingPaths.Union(aiTile.incomingPaths).Where(x => x.type == ImperialMovementConnection.PendulumZipline))
                    {
                        connections.Add(conn);
                    }
                }

                sLeaser.sprites = new FSprite[connections.Count];
                for (int i = 0; i < sLeaser.sprites.Length; i++)
                {
                    sLeaser.sprites[i] = new FSprite("pixel");
                    sLeaser.sprites[i].anchorY = 0f;
                }

                AddToContainer(sLeaser, rCam, null);
            }

            public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
            {
                base.DrawSprites(sLeaser, rCam, timeStacker, camPos);

                if (!initialized && room != null && room.readyForAI)
                {
                    initialized = true;
                    InitiateSprites(sLeaser, rCam);
                }

                if (!initialized) return;

                int i = 0;
                foreach (MovementConnection connection in connections)
                {
                    i++;
                    if (i >= sLeaser.sprites.Length) break;

                    Vector2 a = room.MiddleOfTile(connection.startCoord);
                    Vector2 b = room.MiddleOfTile(connection.destinationCoord);
                    sLeaser.sprites[i].x = b.x - camPos.x;
                    sLeaser.sprites[i].y = b.y - camPos.y;
                    sLeaser.sprites[i].scaleY = Vector2.Distance(b, a);
                    sLeaser.sprites[i].rotation = Custom.AimFromOneVectorToAnother(b, a);
                }
            }
        }
    }
}