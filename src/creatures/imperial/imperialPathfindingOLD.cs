using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using UnityEngine;

namespace Scavolution
{

    // good ole reliable hack
    public static class ImperialMovementConnection
    {
        public static readonly MovementConnection.MovementType SwingDetour = (MovementConnection.MovementType)"SwingDetour".GetHashCode();
    }
    

    public class SwingPathingVisualizer : UpdatableAndDeletable, IDrawable
    {
        public SwingPathingAssistor swingPather;
        public SwingPathingVisualizer(SwingPathingAssistor swingPather) : base()
        {
            this.swingPather = swingPather;
        }

        public void Update()
        {
            
        }

        public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            // throw new NotImplementedException();
        }

        public float thickness = 1f;
        public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
        {
            var detours = swingPather.checkNextList.Select(x => x.Item1).Take(20).ToArray();
            // int confirmeddetours = Mathf.Min(swingPather.checkNextList.Count, 20);
            if (sLeaser.sprites is null || sLeaser.sprites.Length != detours.Length)
            {
                if (sLeaser.sprites is not null && detours.Length < sLeaser.sprites.Length)
                {
                    for (int i = sLeaser.sprites.Length - 1; i >= detours.Length; i--)
                    {
                        sLeaser.sprites[i].RemoveFromContainer();
                    }
                }
                
                Array.Resize(ref sLeaser.sprites, detours.Length);
            }

            var container = rCam.ReturnFContainer("Foreground");
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                if (sLeaser.sprites[i] is null)
                {
                    sLeaser.sprites[i] = TriangleMesh.MakeLongMesh(40, pointyTip: false, customColor: true);
                    container.AddChild(sLeaser.sprites[i]);
                }
            }

            for (int i = 0; i < detours.Length; i++)
            {

                var triMesh = (TriangleMesh)sLeaser.sprites[i];
                // if (i >= confirmeddetours)
                // {
                //     triMesh.color = Color.red;
                // }


                Vector2 currentPartPos(int j)
                {
                    SwingPathingAssistor.Node node = detours[i];
                    if (node is null) return Vector2.zero;
                    while (j-- > 0)
                    {
                        if (node.prevNode is null) break;
                        node = node.prevNode;
                    }

                    return node.pos;
                }

                triMesh.SetPosition(Vector2.zero);
                for (int j = 0; j < 40; j++)
                {

                    int partIndex = j * 4;
                    Vector2 partPos = currentPartPos(j);
                    if (j == 0)
                    {
                        Vector2 followingPartPos = currentPartPos(j + 1);
                        Vector2 normalized = Custom.DirVec(partPos, followingPartPos);
                        Vector2 perpendicularVec = Vector2.Perpendicular(normalized);
                        triMesh.MoveVertice(partIndex + 0, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 1, (partPos + perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 2, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 3, (partPos + perpendicularVec * thickness) - camPos);
                    }
                    else
                    {
                        Vector2 formerPartPos = currentPartPos(Math.Max(j - 1, 0));
                        Vector2 normalized = Custom.DirVec(formerPartPos, partPos);
                        Vector2 perpendicularVec = Vector2.Perpendicular(normalized);
                        triMesh.MoveVertice(partIndex + 0, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 1, (partPos + perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 2, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 3, (partPos + perpendicularVec * thickness) - camPos);
                    }
                }
            }
        }

        public void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            // throw new NotImplementedException();
        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            // throw new NotImplementedException();
        }
    }

    public class SwingPathingAssistor : AIModule
    {

        public const int stepsPerFrame = 5;
        public const int latticeTimeResolution = 1;
        public class Node
        {
            public PathCost costFromOrigin = new PathCost(0, PathCost.Legality.Allowed);
            public PathCost heuristic = new PathCost(0, PathCost.Legality.Allowed);
            public Node? prevNode;
            public Action? fromAction;
            public List<Node> possibleContinuities;

            public int generation = 0;
            public bool beingChecked;
            public bool isBegin;
            public bool isEnd;
            public Vector2 vel;
            public Vector2 pos;
            public Node(Node? prevNode, Action? fromAction)
            {
                this.prevNode = prevNode;
                this.fromAction = fromAction;
                this.possibleContinuities = new();
            }
        }

        public struct Action
        {
            public int minActionConitinuity = 0;
            public int xInput = 0;
            public Vector2? swingAnchor;
            public Action() { }
        }

        public StandardPather standardPather => (StandardPather)AI.pathFinder;
        public PathfinderResourceDivider resourceDivider;
        public WorldCoordinate currentPathPos = new WorldCoordinate(-1, -1, -1, -1);
        public ScavengerAI scavAI => (ScavengerAI)AI;

        public MovementConnection PathFinder_ConnectionAtCoordinate(On.PathFinder.orig_ConnectionAtCoordinate orig, PathFinder self, bool outGoing, WorldCoordinate coord, int index)
        {
            AItile aItile = self.AITileAtWorldCoordinate(coord);
            foreach (MovementConnection conn in completedDetours.Keys)
            {
                var movcoord = outGoing ? conn.startCoord : conn.destinationCoord;
                if (movcoord == coord)
                {
                    if (index == 0)
                    {
                        return conn;
                    }
                    index--;
                }
            }


            return orig(self, outGoing, coord, index);
        }

        public SwingPathingAssistor(ArtificialIntelligence AI) : base(AI)
        {
            resourceDivider = standardPather.world.game.pathfinderResourceDivider;
            standardPather.visualizePath = true;
        }

        public SwingPathingVisualizer? visualizer = null;

        public Dictionary<WorldCoordinate, Node> detourRoutes = new();
        public Dictionary<MovementConnection, Node> completedDetours = new();
        public MovementConnection StandardPather_FollowPath(On.StandardPather.orig_FollowPath orig,
            StandardPather self, WorldCoordinate originPos, bool actuallyFollowingThisPath)
        {
            MovementConnection conn = orig(self, originPos, actuallyFollowingThisPath);
            if (currentPathPos != originPos)
            {
                currentPathPos = originPos;
                MovementConnection nextconn = conn;
                for (int i = 0; i < 6; i++)
                {
                    if (nextconn == default) break;
                    AttemptDetour(nextconn.destinationCoord);
                    nextconn = orig(self, nextconn.destinationCoord, false);
                }

                if (conn.type == ImperialMovementConnection.SwingDetour && actuallyFollowingThisPath)
                {
                    ScavolutionPlugin.pubLogger?.LogDebug("WE ARE SWINGING");
                }
            }

            return conn;
        }

        public void AttemptDetour(WorldCoordinate coord)
        {
            if (standardPather.realizedRoom is Room room)
            {
                if (!detourRoutes.ContainsKey(coord))
                {
                    ScavolutionPlugin.pubLogger?.LogDebug($"BEGINNING DETOUR ROUTE!!!! {coord}");
                    var detour = new Node(null, null);
                    detour.pos = room.MiddleOfTile(coord);
                    detour.vel = Vector2.zero; // TODO: use scav current vel;
                    detour.costFromOrigin = new PathCost(3f, PathCost.Legality.Allowed);
                    detour.isBegin = true;
                    detour.generation = 0;
                    detourRoutes.Add(coord, detour);
                    AddToNextCheckList(detour);
                }
            }
        }

        public void PathFinder_Reset(On.PathFinder.orig_Reset orig, PathFinder self, Room newRealizedRoom)
        {
            orig(self, newRealizedRoom);
            if (visualizer != null)
            {
                visualizer.Destroy();
                visualizer = null;
            }
            visualizer = new SwingPathingVisualizer(this);
            newRealizedRoom.AddObject(visualizer);

            detourRoutes.Clear();
            completedDetours.Clear();
            checkNextList.Clear();
        }

        public void PathFinder_AssignNewDestination(On.PathFinder.orig_AssignNewDestination orig, PathFinder self, WorldCoordinate newDestination)
        {
            orig(self, newDestination);
            checkNextList.Clear();
        }


        public List<(Node node, PathCost value)> checkNextList = new();
        public void AddToNextCheckList(Node addedNode)
        {
            if (addedNode.costFromOrigin.resistance > 30)
            {
                return;
            }

            if (addedNode.beingChecked) return;
            int i = 0;
            PathCost totalCost = addedNode.costFromOrigin + addedNode.heuristic;
            foreach ((Node node, PathCost value) in checkNextList)
            {
                if (addedNode.generation > node.generation) break;
                if (addedNode.generation == node.generation)
                {
                    if (totalCost <= value) break;
                }
                i++;
            }

            addedNode.beingChecked = true;
            checkNextList.Insert(i, (addedNode, totalCost));
        }

        public override void Update()
        {
            if (standardPather.realizedRoom is null) return;
            if (!detourRoutes.ContainsKey(AI.creature.pos))
            {
                AttemptDetour(AI.creature.pos);
            }

            if (checkNextList.Count > 0)
            {
                int count = resourceDivider.RequesAccesibilityUpdates(stepsPerFrame);
                for (int i = 0; i < count; i++)
                {
                    if (!checkNextList.Any()) break;
                    (Node checkedNode, PathCost cost) = checkNextList.First();
                    checkNextList.RemoveAt(0);
                    ScavolutionPlugin.pubLogger?.LogDebug($"node at top of checkednode gen: {checkedNode.generation} cost: {cost}");

                    foreach (Node node in BuildActionMap(checkedNode).Select(action => IntegrateAction(checkedNode, action)).OfType<Node>())
                    {
                        IntVector2 coordOfNode = standardPather.realizedRoom.GetTilePosition(node.pos);
                        if (standardPather.realizedRoom.aimap.TileAccessibleToCreature(coordOfNode, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger)) || node.isEnd)
                        {
                            var base_node = checkedNode;
                            var origWC = standardPather.realizedRoom.GetWorldCoordinate(base_node.pos - Vector2.down * 20f);
                            var destWC = standardPather.realizedRoom.GetWorldCoordinate(node.pos - Vector2.down * 20f);
                            var conn = new MovementConnection(ImperialMovementConnection.SwingDetour,
                                origWC,
                                destWC,
                                (int)node.costFromOrigin.resistance
                            );


                            // if (standardPather.CoordinateCost(conn.destinationCoord).legality <= PathCost.Legality.Unwanted)
                            // {
                            if (origWC.Tile.FloatDist(destWC.Tile) > 4f && !completedDetours.ContainsKey(conn))
                            {
                                ScavolutionPlugin.pubLogger?.LogDebug($"FINISHED DETOUR ROUTE!!!! ({conn.StartTile}) -> ({conn.DestTile})");
                                ScavolutionPlugin.pubLogger?.LogDebug(conn.distance);
                                ScavolutionPlugin.pubLogger?.LogDebug(standardPather.realizedRoom.aimap.IsConnectionAllowedForCreature(conn, standardPather.creatureType));
                                ScavolutionPlugin.pubLogger?.LogDebug(standardPather.creatureType.ConnectionResistance(conn.type));
                                completedDetours.Add(conn, node);


                                ScavolutionPlugin.pubLogger?.LogDebug($"ALL CONNECTIONS AT ({conn.DestTile})");
                                MovementConnection conn2;
                                for (int k = 0; (conn2 = standardPather.ConnectionAtCoordinate(false, conn.destinationCoord, k)) != default; k++)
                                {
                                    ScavolutionPlugin.pubLogger?.LogDebug(Enum.GetName(typeof(MovementConnection.MovementType), conn2.type) ?? "SwingDetour");
                                }

                                if (standardPather.DoneMappingAccessibility)
                                {
                                    standardPather.InitiAccessibilityMapping(
                                        AI.creature.pos, Flatten(standardPather.CurrentRoomCells).OfType<PathFinder.PathingCell>().Where(x => x.reachable && x.possibleToGetBackFrom).Select(x =>
                                            new IntVector2(x.worldCoordinate.x, x.worldCoordinate.y)).ToArray()
                                    );
                                }
                            }
                            // }
                        }
                        AddToNextCheckList(node);
                    }
                }
            }
        }

        public static IEnumerable<Node> EnumerateNodes(Node? node)
        {
            while (node != null)
            {
                Node retnode = node;
                node = node.prevNode;
                yield return retnode;
            }
        }

        public Node? FollowPath(MovementConnection connection)
        {
            foreach ((MovementConnection conn, Node node) in completedDetours.Select(x => (x.Key, x.Value)))
            {
                if (conn != connection) continue;
                return node;
            }

            return null;
        }
        
        public IEnumerable<T> Flatten<T>(T[,] array)
        {
            foreach (T i in array)
            {
                yield return i;
            }
        }



        public List<Action> BuildActionMap(Node node)
        {
            List<Action> actions = [];
            if (node.isEnd) return actions;
            if (node.fromAction is Action fromAction)
            {
                Action copy = new();
                copy.minActionConitinuity = Mathf.Max(copy.minActionConitinuity - 1, 0);
                copy.swingAnchor = fromAction.swingAnchor;
                copy.xInput = fromAction.xInput;
                actions.Add(copy);

                if (fromAction.minActionConitinuity > 0)
                {
                    return actions;
                }
            }

            if (!node.isBegin)
            {
                AddActionsForAnchor(actions, null, 3);
            }
            foreach (Vector2 ceiling in RayTraceCeilings(node.pos))
            {
                AddActionsForAnchor(actions, ceiling, 3);
            }

            return actions;
        }


        const float minPendulumRange = 4 * 20f;
        const float pendulumRange = 100f * 20f;
        public List<Vector2> RayTraceCeilings(Vector2 where)
        {
            if (standardPather.realizedRoom is null) return [];
            const int rays = 11;
            float range = 45;
            List<Vector2> hits = new();
            for (int i = 0; i < rays; i++)
            {
                int ray = i - 1 - (rays / 2);
                Vector2 dir = Custom.rotateVectorDeg(Vector2.up, ray * (range / (float)rays));
                Vector2? rayhit = SharedPhysics.ExactTerrainRayTracePos(standardPather.realizedRoom, where, dir * pendulumRange + where);
                if (rayhit.HasValue && !Custom.DistLess(rayhit.Value, where, minPendulumRange))
                {
                    hits.Add(rayhit.Value);
                }
            }
            return hits;
        }

        public void AddActionsForAnchor(List<Action> actions, Vector2? anchor, int minActionConitinuity = 5)
        {
            actions.Add(new Action() { minActionConitinuity = minActionConitinuity, swingAnchor = anchor, xInput = 0 });
            actions.Add(new Action() { minActionConitinuity = minActionConitinuity, swingAnchor = anchor, xInput = 1 });
            actions.Add(new Action() { minActionConitinuity = minActionConitinuity, swingAnchor = anchor, xInput = -1 });
        }

        public Node? IntegrateAction(Node previousNode, Action action)
        {
            if (standardPather.realizedRoom is null) return null;
            Room room = standardPather.realizedRoom;
            Node node = new(previousNode, action);
            node.pos = previousNode.pos;
            node.vel = previousNode.vel;
            node.generation = previousNode.generation + 1;

            for (int i = 0; i < latticeTimeResolution; i++)
            {
                Vector2 prePos = node.pos;
                node.pos = node.pos + node.vel;
                node.vel += standardPather.realizedRoom.gravity * Vector2.down;

                // integrate action.
                if (action.xInput != 0)
                {
                    if (action.swingAnchor.HasValue)
                    {
                        Vector2 tangent = Custom.rotateVectorDeg(action.swingAnchor.Value - node.pos, 90f);
                        node.vel += 1.0f * tangent;
                    }
                    else
                    {
                        node.vel.x += 0.8f * action.xInput;
                    }
                }

                if (action.swingAnchor.HasValue)
                {
                    Vector2 difference = action.swingAnchor.Value - node.pos;
                    float stretch = difference.magnitude - ImperialPendulum.MAX_ROPELENGTH;
                    if (stretch > 0f)
                    {
                        Vector2 correctionDir = difference.normalized;
                        float alignedSpeed = Vector2.Dot(node.vel, correctionDir);
                        node.vel -= alignedSpeed * correctionDir;
                        node.vel += Mathf.Max(alignedSpeed, 0f) * correctionDir;

                        float positionalRestraint = 0.1f;
                        node.pos += correctionDir * stretch * positionalRestraint;
                        float forceRestraint = 0.01f;
                        node.vel += correctionDir * stretch * forceRestraint;
                    }
                }

                SharedPhysics.TerrainCollisionData cd = new(node.pos, prePos, node.vel, 15f, new IntVector2(0, 0), true);
                SharedPhysics.HorizontalCollision(room, cd);
                SharedPhysics.SlopesVertically(room, cd);
                SharedPhysics.VerticalCollision(room, cd);
                node.pos = cd.pos;
                node.vel = cd.vel;
                if (cd.contactPoint.y == -1)
                {
                    Vector2 normal = -cd.contactPoint.ToVector2();
                    if (Vector2.Dot(normal, node.vel) < 0)
                    {
                        node.isEnd = true;
                        break;
                    }
                }
            }

            node.costFromOrigin = previousNode.costFromOrigin + new PathCost(BiasedDistanceFunc(previousNode.pos, node.pos), PathCost.Legality.Allowed);
            node.heuristic = HueristicForNode(node);
            return node;
        }

        public float ydistMultiplier = 0.15f;
        public float BiasedDistanceFunc(Vector2 A, Vector2 B)
        {
            // no bias for now :'(
            // float xdist = (B.x - A.x) * 0.85f;
            // float ydist = (B.y - A.y) * ydistMultiplier;
            return Mathf.Sqrt(A.x * A.x + A.y * A.y);
        }

        public PathCost HueristicForNode(Node node)
        {
            if (standardPather.realizedRoom is null) return new PathCost(0f, PathCost.Legality.Unallowed);
            if (standardPather.room != standardPather.currentlyFollowingDestination.room) return new PathCost(0f, PathCost.Legality.Unallowed);
            if (standardPather.currentlyFollowingDestination.NodeDefined) return new PathCost(0f, PathCost.Legality.Unallowed); 

            Vector2 dest = standardPather.realizedRoom.MiddleOfTile(standardPather.currentlyFollowingDestination);
            float dist = BiasedDistanceFunc(dest, node.pos);
            // float veldist = Mathf.Min(Vector2.Dot(node.vel, (dest - standardPather.realizedRoom.MiddleOfTile(currentPathPos)).normalized), 100f);
            // dist = dist * (1.0f - 0.4f * veldist / 100f);
            return new PathCost(dist, PathCost.Legality.Allowed);
        }
    }

    partial class ScavolutionPlugin
    {
        public void ImperialPathfindingHooksOLD()
        {
            Logger.LogDebug("Congrats on finding the lost pathfinding code! I might do something with this someday... check back later");
            On.ScavengerAI.ctor += ImperialPathfinding_ScavengerAI_ctor;
            On.StandardPather.FollowPath += (On.StandardPather.orig_FollowPath orig, StandardPather self, WorldCoordinate originPos, bool actuallyFollowingThisPath) =>
            {
                if (self.AI.modules.OfType<SwingPathingAssistor>().FirstOrDefault() is SwingPathingAssistor pather) return pather.StandardPather_FollowPath(orig, self, originPos, actuallyFollowingThisPath);
                return orig(self, originPos, actuallyFollowingThisPath);
            };

            On.PathFinder.AssignNewDestination += (On.PathFinder.orig_AssignNewDestination orig, PathFinder self, WorldCoordinate dest) =>
            {
                if (self.AI.modules.OfType<SwingPathingAssistor>().FirstOrDefault() is SwingPathingAssistor pather)
                {
                    pather.PathFinder_AssignNewDestination(orig, self, dest);
                }
                else
                {
                    orig(self, dest);
                }
            };

            On.CreatureTemplate.ConnectionResistance += (On.CreatureTemplate.orig_ConnectionResistance orig, CreatureTemplate self, MovementConnection.MovementType type) =>
            {
                if (type == ImperialMovementConnection.SwingDetour)
                {
                    if (self.type == SECreatureEnums.ScavengerImperial) return new PathCost(0f, PathCost.Legality.Allowed);
                    return new PathCost(0f, PathCost.Legality.IllegalConnection);
                }

                return orig(self, type);
            };
            
            On.AImap.IsConnectionForceAllowedForCreature += AIMap_IsConnectionForceAllowedForCreature;

            On.PathFinder.Reset += (On.PathFinder.orig_Reset orig, PathFinder self, Room newRealizedRoom) =>
            {
                if (self.AI.modules.OfType<SwingPathingAssistor>().FirstOrDefault() is SwingPathingAssistor pather)
                {
                    pather.PathFinder_Reset(orig, self, newRealizedRoom);
                }
                else
                {
                    orig(self, newRealizedRoom);
                }
            };

            On.ScavengerAI.TravelPreference += ImperialPathfinding_ScavengerAI_TravelPreference;
            On.PathFinder.ConnectionAtCoordinate += (On.PathFinder.orig_ConnectionAtCoordinate orig, PathFinder self, bool outGoing, WorldCoordinate coord, int index) =>
            {
                if (self.AI.modules.OfType<SwingPathingAssistor>().FirstOrDefault() is SwingPathingAssistor pather)
                {
                    return pather.PathFinder_ConnectionAtCoordinate(orig, self, outGoing, coord, index);
                }
                else
                {
                    return orig(self, outGoing, coord, index);
                }
            };
        }
        
        
        bool AIMap_IsConnectionForceAllowedForCreature(On.AImap.orig_IsConnectionForceAllowedForCreature orig, global::AImap self, global::MovementConnection connection, global::CreatureTemplate crit, out bool forceAllow)
        {
            if (crit.type == SECreatureEnums.ScavengerImperial)
            {
                if (connection.IsDrop)
                {
                    forceAllow = true;
                    return true;
                }

                if (connection.type == ImperialMovementConnection.SwingDetour)
                {
                    forceAllow = true;
                    return true;
                }

                // if (self.getAItile(connection.destinationCoord).acc == AItile.Accessibility.Air)
                // {
                //     forceAllow = true;
                //     return true;
                // }
            }
            return orig(self, connection, crit, out forceAllow);
        }

        void ImperialPathfinding_ScavengerAI_ctor(On.ScavengerAI.orig_ctor orig, ScavengerAI self, AbstractCreature creature, World world)
        {
            orig(self, creature, world);
            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (creature.creatureTemplate.type == SECreatureEnums.ScavengerImperial)
                {
                    self.AddModule(new SwingPathingAssistor(self));
                    // if (self.obstacleTracker is null) self.AddModule(new ObstacleTracker(self, false, true, 0, 1, 3));
                }
            }

        }

        PathCost ImperialPathfinding_ScavengerAI_TravelPreference(On.ScavengerAI.orig_TravelPreference orig, ScavengerAI self, MovementConnection conn, PathCost cost)
        {
            // if (self.obstacleTracker != null && self.scavenger.isImperial())
            // {
            //     cost += new PathCost(Mathf.Pow(self.obstacleTracker.ObstacleWarning(conn), 3f) * 5f, PathCost.Legality.Allowed);
            // }

            return orig(self, conn, cost);
        }
    }
}