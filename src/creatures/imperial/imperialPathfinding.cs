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

    partial class ScavolutionPlugin
    {
        public void ImperialPathfindingHooks()
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
                    // self.AddModule(new SwingPathingAssistor(self));
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

        public class SwingPathingVisualizer : UpdatableAndDeletable, IDrawable
        {
            public SwingPathingAssistor swingPather;
            public SwingPathingVisualizer(SwingPathingAssistor swingPather) : base()
            {
                this.swingPather = swingPather;
            }

            // int connectionHash = 0;

            public bool initialized = false;
            public int drawnCamPos = -1;
            public bool devactive = true;
            public void Update()
            {
                
            }

            public void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
            {
                // throw new NotImplementedException();
            }

            public void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
            {
                // throw new NotImplementedException();
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

            public int stepsPerFrame = 5;
            public int latticeTimeResolution = 5;
            public class Node
            {
                public PathCost costFromOrigin = new PathCost(0, PathCost.Legality.Allowed);
                public PathCost heuristic  = new PathCost(0, PathCost.Legality.Allowed);
                public Node? prevNode;
                public Action? fromAction;
                public List<Node> possibleContinuities;

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
                if (coord.TileDefined)
                {
                    List<MovementConnection> list  = ((!outGoing) ? aItile.incomingPaths : aItile.outgoingPaths);
                    if (index > list.Count)
                    {
                        int swingIndex = index - list.Count - 1; // list.Count is a used index as well.
                        foreach (MovementConnection conn in completedDetours.Keys)
                        {
                            var movcoord = outGoing ? conn.startCoord : conn.destinationCoord; 
                            if (movcoord == coord)
                            {
                                swingIndex--;
                                if (swingIndex < 0)
                                {
                                    return conn;
                                }
                            }
                        }
                    }
                }

                
                return orig(self, outGoing, coord, index);
            }

            public SwingPathingAssistor(ArtificialIntelligence AI) : base(AI)
            {
                resourceDivider = standardPather.world.game.pathfinderResourceDivider;
                // standardPather.visualizePath = true;
            }

            public SwingPathingVisualizer? visualizer = null;

            public Dictionary<WorldCoordinate, Node> detourRoutes = new();
            public Dictionary<MovementConnection, Node> completedDetours = new();
            public MovementConnection StandardPather_FollowPath(On.StandardPather.orig_FollowPath orig,
                StandardPather self, WorldCoordinate originPos, bool actuallyFollowingThisPath)
            {

                if (standardPather.realizedRoom is Room room)
                {
                    if (visualizer is null)
                    {
                        visualizer = new SwingPathingVisualizer(this);
                        room.AddObject(visualizer);
                    }
                    if (!detourRoutes.ContainsKey(originPos))
                    {
                        pubLogger?.LogDebug($"BEGINNING DETOUR ROUTE!!!! {originPos}");
                        var detour = new Node(null, null);
                        detour.pos = room.MiddleOfTile(originPos);
                        detour.vel = Vector2.zero; // TODO: use scav current vel;
                        detour.costFromOrigin = new PathCost(600f, PathCost.Legality.Allowed);
                        detourRoutes.Add(originPos, detour);
                        AddToNextCheckList(detour);
                    }
                }



                if (actuallyFollowingThisPath) currentPathPos = originPos;
                return orig(self, originPos, actuallyFollowingThisPath);
            }

            
            public void PathFinder_Reset(On.PathFinder.orig_Reset orig, PathFinder self, Room newRealizedRoom)
            {
                orig(self, newRealizedRoom);
                if (visualizer != null)
                {
                    visualizer.Destroy();
                    visualizer = null;
                }    
                detourRoutes.Clear();
                completedDetours.Clear();
                checkNextList.Clear();
            }

            public void PathFinder_AssignNewDestination(On.PathFinder.orig_AssignNewDestination orig, PathFinder self, WorldCoordinate newDestination)
            {
                orig(self, newDestination);
                checkNextList.Clear();
            }


            public List<(Node, PathCost value)> checkNextList = new();
            public void AddToNextCheckList(Node node)
            {
                int i = 0;
                PathCost totalCost = node.costFromOrigin + node.heuristic;
                foreach ((_, PathCost value) in checkNextList)
                {
                    if (totalCost < value) break;
                    i++;
                }

                checkNextList.Insert(i, (node, totalCost));
            }

            public override void Update()
            {
                if (standardPather.realizedRoom is null) return;
                if (checkNextList.Count > 0)
                {
                    int count = resourceDivider.RequesAccesibilityUpdates(Math.Min(checkNextList.Count, stepsPerFrame));
                    for (int i = 0; i < count; i++)
                    {
                        if (!checkNextList.Any()) continue;
                        (Node checkedNode, _) = checkNextList.First();
                        checkNextList.RemoveAt(0);
                        foreach (Node node in BuildActionMap(checkedNode).Select(action => IntegrateAction(checkedNode, action)).OfType<Node>())
                        {
                            IntVector2 coordOfNode = standardPather.realizedRoom.GetTilePosition(node.pos);
                            if (standardPather.realizedRoom.aimap.TileAccessibleToCreature(coordOfNode, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger)) || node.isEnd)
                            {
                                var base_node = node;
                                while (base_node.prevNode != null) base_node = base_node.prevNode;
                                var origWC = standardPather.realizedRoom.GetWorldCoordinate(base_node.pos);
                                var destWC = standardPather.realizedRoom.GetWorldCoordinate(node.pos);
                                var conn = new MovementConnection(ImperialMovementConnection.SwingDetour,
                                    origWC,
                                    destWC,
                                    (int)node.costFromOrigin.resistance
                                );
                                if (origWC.Tile.FloatDist(destWC.Tile) > 4f && !completedDetours.ContainsKey(conn))
                                {
                                    pubLogger?.LogDebug($"FINISHED DETOUR ROUTE!!!! ({ conn.StartTile }) -> ({ conn.DestTile })");
                                    completedDetours.Add(conn, base_node);
                                }
                            }
                            AddToNextCheckList(node);
                        }
                    }
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

                AddActionsForAnchor(actions, null, 5);
                foreach (Vector2 ceiling in RayTraceCeilings(node.pos))
                {
                    AddActionsForAnchor(actions, ceiling, 5);
                }

                return actions;
            }


            const float minPendulumRange = 4 * 20f;
            const float pendulumRange = 36f * 20f;
            public List<Vector2> RayTraceCeilings(Vector2 where)
            {
                if (standardPather.realizedRoom is null) return [];
                const int rays = 7;
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

                for (int i = 0; i < latticeTimeResolution; i++)
                {
                    Vector2 prePos = node.pos;
                    node.pos = node.pos + node.vel;
                    node.vel += standardPather.realizedRoom.gravity * 0.9f * Vector2.down;

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

                    SharedPhysics.TerrainCollisionData cd = new(node.pos, prePos, node.vel, 5f, new IntVector2(0, 0), true);
                    SharedPhysics.HorizontalCollision(room, cd);
                    SharedPhysics.VerticalCollision(room, cd);
                    if (cd.contactPoint.x != 0 && cd.contactPoint.y != 0)
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
                float xdist = (B.x - A.x) * 0.85f;
                float ydist = (B.y - A.y) * ydistMultiplier;
                return Mathf.Sqrt(xdist * xdist + ydist * ydist);
            }

            public PathCost HueristicForNode(Node node)
            {
                if (standardPather.realizedRoom is null) return new PathCost(0f, PathCost.Legality.Unallowed);
                if (standardPather.room != standardPather.currentlyFollowingDestination.room) return new PathCost(0f, PathCost.Legality.Unallowed);
                if (standardPather.currentlyFollowingDestination.NodeDefined) return new PathCost(0f, PathCost.Legality.Unallowed); 

                Vector2 dest = standardPather.realizedRoom.MiddleOfTile(standardPather.currentlyFollowingDestination);
                float dist = BiasedDistanceFunc(dest, node.pos);
                float veldist = Mathf.Min(Vector2.Dot(node.vel, (dest - standardPather.realizedRoom.MiddleOfTile(currentPathPos)).normalized), 100f);
                dist = dist * (1.0f - 0.4f * veldist / 100f);
                return new PathCost(dist, PathCost.Legality.Allowed);
            }
        }
    }
}