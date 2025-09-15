using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Mono.Cecil.Cil;
using MonoMod.Cil;
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
                    if (self.type == SECreatureEnums.ScavengerImperial)
                    {
                        if (type == ImperialMovementConnection.EnterPendulumZipline) new PathCost(50f, PathCost.Legality.Allowed);
                        if (type == ImperialMovementConnection.PendulumZipline) new PathCost(1f, PathCost.Legality.Allowed);
                    }
                    return new PathCost(0f, PathCost.Legality.IllegalConnection); 
                }

                return orig(self, type);
            };

            // IL.World.ctor += (ILContext context) =>
            // {
            //     try
            //     {
            //         ILCursor cursor = new ILCursor(context);
            //         cursor.GotoNext(MoveType.Before, x => x.MatchStfld<World>(nameof(World.preProcessingGeneration)));
            //         cursor.Emit(OpCodes.Pop);
            //         cursor.Emit(OpCodes.Ldc_I4, 21);
            //     }
            //     catch (Exception except)
            //     {
            //         Logger.LogError(except);
            //     }
            // };



            IL.AImap.TileAccessibleToCreature_IntVector2_CreatureTemplate += (ILContext context) =>
            {
                try
                {
                    // 43	0060	ldarg.0
                    // 44	0061	ldarg.1
                    // 45	0062	ldarg.2
                    // 46	0063	ldloca.s	V_1 (1)
                    // 47	0065	call	instance bool AImap::IsTooCloseToTerrain(valuetype RWCustom.IntVector2, class CreatureTemplate, bool&)
                    // 48	006A	brfalse.s	51 (006E) ldsfld bool ModManager::MMF
                    ILCursor cursor = new(context);
                    ILLabel label = null!;
                    cursor.GotoNext(
                        x => x.MatchLdarg(0),
                        x => x.MatchLdarg(1),
                        x => x.MatchLdarg(2),
                        x => x.MatchLdloca(1),
                        x => x.MatchCall<AImap>(nameof(AImap.IsTooCloseToTerrain)),
                        x => x.MatchBrfalse(out label)
                    );

                    cursor.Goto(label.Target);
                    cursor.MoveAfterLabels();
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.Emit(OpCodes.Ldarg_1);
                    cursor.Emit(OpCodes.Ldarg_2);
                    cursor.EmitDelegate((AImap self, IntVector2 pos, CreatureTemplate crit) =>
                    {
                        // swinging is only a connection between two points, not somewhere we can spawn.
                        return self.getAItile(pos).outgoingPaths.All(path =>
                        {
                            return path.type == ImperialMovementConnection.EnterPendulumZipline || path.type == ImperialMovementConnection.PendulumZipline;
                        });
                    });
                    ILLabel label2 = cursor.DefineLabel();
                    cursor.Emit(OpCodes.Brfalse, label2);
                    cursor.Emit(OpCodes.Ldc_I4_0);
                    cursor.Emit(OpCodes.Ret);
                    cursor.MarkLabel(label2);
                }
                catch (Exception except)
                {
                    Logger.LogError(except);
                }

            };


            On.AImap.IsConnectionForceAllowedForCreature += (On.AImap.orig_IsConnectionForceAllowedForCreature orig, global::AImap self, global::MovementConnection connection, global::CreatureTemplate crit, out bool forceAllow) =>
            {
                if (crit.type == SECreatureEnums.ScavengerImperial)
                {
                    if (connection.IsDrop && IsPendulumZiplineTile(self.getAItile(connection.StartTile), connection.StartTile, self.room) && crit.type == SECreatureEnums.ScavengerImperial)
                    {
                        forceAllow = true;
                        return true;
                    }

                    if (connection.type == ImperialMovementConnection.PendulumZipline)
                    {
                        forceAllow = true;
                        return true;
                    }

                    if (connection.type == ImperialMovementConnection.EnterPendulumZipline)
                    {
                        forceAllow = true;
                        return true;
                    }
                }
                return orig(self, connection, crit, out forceAllow);
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
                if (IsPendulumZiplineTile(self.map.getAItile(currentPos), currentPos, self.room))
                {
                    Logger.LogDebug($"Found Zipline tile, ({currentPos.x}, {currentPos.y})");

                    // add floor drop connections
                    for (int j = currentPos.y - 1; j > 0; j--)
                    {
                        if ((self.map.getAItile(currentPos.x, j).acc == AItile.Accessibility.Floor ||
                            self.map.getAItile(currentPos.x, j).acc == AItile.Accessibility.CurvedFloor) &&
                             (self.room.GetTile(currentPos.x, j - 1).Terrain == Room.Tile.TerrainType.Solid ||
                              self.room.GetTile(currentPos.x, j - 1).Terrain == Room.Tile.TerrainType.Floor ||
                              (self.room.terrain != null && self.room.terrain.ObstructsTile(currentPos.x, j - 1))))
                        {
                            self.map.map[self.x, self.y].outgoingPaths.Add(new MovementConnection(MovementConnection.MovementType.DropToFloor, self.WrldCrd(currentPos), self.WrldCrd(new IntVector2(currentPos.x, j)), currentPos.y - j));
                            break;
                        }
                        if (self.map.getAItile(currentPos.x, j).acc == AItile.Accessibility.Climb &&
                           self.map.getAItile(currentPos.x, j + 1).acc > self.map.getAItile(currentPos.x, j).acc)
                        {
                            self.map.map[self.x, self.y].outgoingPaths.Add(new MovementConnection(MovementConnection.MovementType.DropToClimb, self.WrldCrd(currentPos), self.WrldCrd(new IntVector2(currentPos.x, j)), currentPos.y - j));
                        }
                        if (self.room.GetTile(currentPos.x, j).WaterSurface)
                        {
                            self.map.map[self.x, self.y].outgoingPaths.Add(new MovementConnection(MovementConnection.MovementType.DropToWater, self.WrldCrd(currentPos), self.WrldCrd(new IntVector2(currentPos.x, j)), currentPos.y - j));
                        }
                    }

                    int foundneighbors = 0;
                    foreach (IntVector2 neighbor in Custom.eightDirections.Select(x => x + currentPos))
                    {
                        if (IsPendulumZiplineTile(self.map.getAItile(neighbor), neighbor, self.room))
                        {
                            ++foundneighbors;
                            var newConn = new MovementConnection(
                                ImperialMovementConnection.PendulumZipline, self.WrldCrd(currentPos), self.WrldCrd(neighbor), 1
                            );

                            self.map.getAItile(currentPos).outgoingPaths.Add(newConn);
                            self.map.getAItile(neighbor).incomingPaths.Add(newConn);
                        }
                    }

                    RoomExtensions.map.GetValue(self.room, x => new RoomExtensions(x)).ziplineTiles.Add(currentPos);
                    Logger.LogDebug($"Found {foundneighbors} neighbors ]:()");
                }
                else
                {
                    List<IntVector2> checkedTiles = null!;
                    FloodSearch(self.map, currentPos, [Custom.leftRightUpDown[2], Custom.leftRightUpDown[0], Custom.leftRightUpDown[1],], ref checkedTiles, (map, tile) =>
                    {
                        if (map.room.HasAnySolid(tile)) return true;
                        if (tile.y < currentPos.y) return true;
                        if (currentPos == tile) return false;
                        if (IsPendulumZiplineTile(map.getAItile(tile), tile, map.room))
                        {
                            Vector2 idealCeiling = map.room.MiddleOfTile(tile + new IntVector2(0, IDEAL_CEILING_DISTANCE));
                            Vector2 resultpos = SharedPhysics.TraceTerrainCollision(self.room, idealCeiling,
                                Vector2.MoveTowards(map.room.MiddleOfTile(currentPos), idealCeiling, 10), 20, true);
                            if (Custom.Dist(idealCeiling, resultpos) > 40f) return false; // 2 tiles

                            var newConn = new MovementConnection(
                                ImperialMovementConnection.EnterPendulumZipline, self.WrldCrd(currentPos), self.WrldCrd(tile), 1
                            );

                            map.getAItile(currentPos).outgoingPaths.Add(newConn);
                            map.getAItile(tile).incomingPaths.Add(newConn);
                            return true;
                        }
                        return false;
                    }, 10);
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void FloodSearch(AImap map, IntVector2 currentTile, IntVector2[] neighbors, ref List<IntVector2> checkedTiles, Func<AImap, IntVector2, bool> func, int maxDistance, IntVector2? originTile = null)
        {
            if (originTile is null) originTile = currentTile;
            if (!Custom.DistLess(originTile.Value, currentTile, maxDistance)) return;
            if (checkedTiles is null) checkedTiles = new();
            if (checkedTiles.Contains(currentTile)) return;

            checkedTiles.Add(currentTile);
            if (func(map, currentTile)) return;

            foreach (IntVector2 neighbor in neighbors)
            {
                IntVector2 neighborTile = neighbor + currentTile;
                if (neighborTile.x >= 0 && neighborTile.x < map.width && neighborTile.y >= 0 && neighborTile.y < map.height)
                {
                    FloodSearch(map, neighborTile, neighbors, ref checkedTiles, func, maxDistance, originTile);
                }
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

        public bool IsValidCeiling(IntVector2 ceiling, Room room)
        {
            bool valid = false;
            valid = valid || room.HasAnySolid(ceiling);
            valid = valid || room.GetTile(ceiling).horizontalBeam;
            return valid;
        }  

        public bool BakeIsPendulumZiplineTile(AItile tile, IntVector2 pos, Room room)
        {
            if (tile.narrowSpace) return false;
            var flooraltitude = Mathf.Max(tile.floorAltitude, tile.smoothedFloorAltitude);

            if (flooraltitude < MIN_FLOOR_DISTANCE) return false;
            if (room.terrain != null && room.terrain.ObstructsTile(pos.x, pos.y)) return false;
            if (room.HasAnySolid(pos.x, pos.y)) return false;
            ref int ceilingdistance = ref AITileExtensions.map.GetValue(tile, x => new AITileExtensions(x)).ceilingdistance;
            if (ceilingdistance < 0)
            {
                ceilingdistance = 0;
                for (int i = pos.y; i < room.Height && !IsValidCeiling(new IntVector2(pos.x, i), room); i++)
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
            public int drawnCamPos = -1;
            public bool devactive = true;
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
                    foreach (MovementConnection conn in aiTile.outgoingPaths.Union(aiTile.incomingPaths).Where(
                        x => x.type == ImperialMovementConnection.EnterPendulumZipline || x.type == ImperialMovementConnection.PendulumZipline))
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

                if (rCam.currentCameraPosition != drawnCamPos || devactive != rCam.game.devToolsActive)
                {
                    drawnCamPos = rCam.currentCameraPosition;
                    devactive = rCam.game.devToolsActive;
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
                        sLeaser.sprites[i].isVisible = devactive != (connection.type == ImperialMovementConnection.PendulumZipline);
                    }
                }
            }
        }
    }
}