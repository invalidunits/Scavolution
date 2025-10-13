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
            On.PathFinder.ctor += ScavengerImperial_Pathfinder_ctor;
        }

        public void ScavengerImperial_Pathfinder_ctor(On.PathFinder.orig_ctor orig, PathFinder self, ArtificialIntelligence AI, World world, AbstractCreature creature)
        {
            orig(self, AI, world, creature);
            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (creature?.creatureTemplate?.type is CreatureTemplate.Type t && t == SECreatureEnums.ScavengerImperial)
                {
                    AI.AddModule(new SwingPathfinderModule(self, AI));
                    // self.visualize = true;
                    // self.visualizePath = true;
                }
            }
        }


        public class SwingPathfinderModule : AIModule
        {
            PathFinder basePathfinder;
            public SwingPathfinderModule(PathFinder basePathfinder, ArtificialIntelligence AI) : base(AI)
            {
                this.basePathfinder = basePathfinder;
            }
            

            public override void Update()
            {
            }

            public override void NewRoom(Room room)
            {
            }
        }
    }
}