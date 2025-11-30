

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HUD;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;

namespace Scavolution
{
    public partial class ScavolutionPlugin
    {
        public void ScavengerAIHooks()
        {
            On.AbstractCreature.WantToStayInDenUntilEndOfCycle += AbstractCreature_WantToStayInDenUntilEndOfCycle;
            On.ScavengerAbstractAI.ReadyToJoinSquad += ScavengerAbstractAI_ReadyToJoinSquad;
            On.ScavengerAbstractAI.ScavengerSquad.DoesScavengerWantToBeInSquad += ScavengerSquad_DoesScavengerWantToBeInSquad;
            On.ScavengerAI.ctor += ScavengerAI_ctor;
            On.ScavengerAbstractAI.GoHome += ScavengerAbstractAI_GoHome;
            IL.ScavengerAI.DecideBehavior += ScavengerAI_DecideBehavior;
            On.ScavengerAI.CollectScore_PhysicalObject_bool += ScavengerAI_CollectScore;
            On.ScavengerAI.RecognizeCreatureAcceptingGift += ScavengerAI_RecognizeCreatureAcceptingGift;

            On.ScavengerAbstractAI.InitGearUp += Scavenger_AbstractScavengerAI_InitGearUP;
            On.ScavengerAbstractAI.ReGearInDen += Scavenger_AbstractScavengerAI_ReGearInDen;
        }

        void Scavenger_AbstractScavengerAI_InitGearUP(On.ScavengerAbstractAI.orig_InitGearUp orig, ScavengerAbstractAI self)
        {
            SetNewGearAsUnevolvable(() => orig(self), self.parent);
        }

        void Scavenger_AbstractScavengerAI_ReGearInDen(On.ScavengerAbstractAI.orig_ReGearInDen orig, ScavengerAbstractAI self)
        {
            SetNewGearAsUnevolvable(() => orig(self), self.parent);
        }


        public sealed class EmptyPlaceholder {}
        public static ConditionalWeakTable<AbstractPhysicalObject, EmptyPlaceholder> cant_evolve_with = new(); // hack

        // don't evolve with gear you took from the stronghold! >:(
        public void SetNewGearAsUnevolvable(Action makeNewGear, AbstractCreature scavenger)
        {
            var currentGear = scavenger.stuckObjects.Where(x => x is AbstractPhysicalObject.CreatureGripStick stick && stick.A == scavenger && stick.B != null);
            makeNewGear();
            var allGear = scavenger.stuckObjects.Where(x => x is AbstractPhysicalObject.CreatureGripStick stick && stick.A == scavenger && stick.B != null);
            var newGear = allGear.Except(currentGear);

            foreach (AbstractPhysicalObject.AbstractObjectStick stick in newGear)
            {
                cant_evolve_with.Add(stick.B, new EmptyPlaceholder());
            }
        }

        public bool ScavengerAbstractAI_GoHome(On.ScavengerAbstractAI.orig_GoHome orig, ScavengerAbstractAI self)
        {

            var tracker = self.GetEvolutionTracker();
            if (tracker.upgradeOpertunity || tracker.successfulUpgrade.HasValue) {
                return true;
            }

            if (self.followCreature is not null)
            {
                if (isPlayer(self.followCreature, out _)) return false;
            }

            if (self.followCreature?.abstractAI is ScavengerAbstractAI scavAI)
            {
                if (scavAI.GoHome()) return true;
            }
            
            return orig(self);
        }


        public void ScavengerAbstractAI_ReGearInDen(On.ScavengerAbstractAI.orig_ReGearInDen orig, ScavengerAbstractAI self)
        {
            try
            {
                self.GetEvolutionTracker().CheckSuccess(true);
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self);
        }

        public bool AbstractCreature_WantToStayInDenUntilEndOfCycle(On.AbstractCreature.orig_WantToStayInDenUntilEndOfCycle orig, global::AbstractCreature self)
        {
            try
            {
                if (self.abstractAI is ScavengerAbstractAI scavAI)
                {
                    var evolution = scavAI.GetEvolutionTracker();
                    if (evolution.upgradeOpertunity || evolution.successfulUpgrade.HasValue)
                    {
                        return true;
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            return orig(self);
        }

        public int ScavengerAI_CollectScore(On.ScavengerAI.orig_CollectScore_PhysicalObject_bool orig, global::ScavengerAI self, PhysicalObject obj, bool weaponFiltered)
        {
            int value = orig(self, obj, weaponFiltered);
            try
            {
                if (!weaponFiltered)
                {
                    if (EvolutionTree.TryGetEvolution(self.creature, obj.abstractPhysicalObject, out var evolution))
                    {
                        return Mathf.Max(value, 7);
                    }
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            return value;
        }

        public bool ScavengerAbstractAI_ReadyToJoinSquad(On.ScavengerAbstractAI.orig_ReadyToJoinSquad orig, global::ScavengerAbstractAI self)
        {
            bool ready = orig(self);
            try
            {
                if (self.GetEvolutionTracker().upgradeOpertunity) return false;
                if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    if (self.followCreature is not null)
                    {
                        if (self.followCreature.creatureTemplate.TopAncestor().type != CreatureTemplate.Type.Scavenger) return false;
                    }
                    
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }


            return ready;
        }

        public bool ScavengerSquad_DoesScavengerWantToBeInSquad(On.ScavengerAbstractAI.ScavengerSquad.orig_DoesScavengerWantToBeInSquad orig, global::ScavengerAbstractAI.ScavengerSquad self, global::ScavengerAbstractAI testScav)
        {
            try
            {
                if (testScav.GetEvolutionTracker().upgradeOpertunity) return false;
                if (testScav.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    if (testScav.followCreature is not null)
                    {
                        if (testScav.followCreature.creatureTemplate.TopAncestor().type != CreatureTemplate.Type.Scavenger) return false;
                        if (!self.members.Contains(testScav.followCreature)) return false;
                    }
                }
                 
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }



            return orig(self, testScav);
        }

        public void ScavengerAI_ctor(On.ScavengerAI.orig_ctor orig, global::ScavengerAI self, global::AbstractCreature creature, global::World world)
        {
            orig(self, creature, world);
            try
            {
                var evolvetracker = new EvolutionTracker(self);
                self.AddModule(evolvetracker);
                self.utilityComparer.AddComparedModule(evolvetracker, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.033333335f), 0.7f, 1.1f);
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        public void ScavengerAI_DecideBehavior(ILContext iL)
        {

            try
            {
                ILCursor cursor = new ILCursor(iL);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((ScavengerAI self) =>
                {
                    var evolutionTracker = ((ScavengerAbstractAI)self.creature.abstractAI).GetEvolutionTracker();
                    evolutionTracker.Update();
                });
                /*
                    45	009B	ldarg.0
                    46	009C	ldfld	float32 ScavengerAI::currentUtility
                    47	00A1	stloc.1
                */
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<ScavengerAI>(nameof(ScavengerAI.currentUtility)),
                    x => x.MatchStloc(1)
                );
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloca, 0);
                cursor.Emit(OpCodes.Ldloca, 1);
                cursor.EmitDelegate((ScavengerAI self, ref AIModule module, ref float utility) =>
                {
                    if (module is EvolutionTracker evo)
                    {
                        self.behavior = ScavengerAI.Behavior.EscapeRain;
                    }
                });
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        
        private void ScavengerAI_RecognizeCreatureAcceptingGift(On.ScavengerAI.orig_RecognizeCreatureAcceptingGift orig, ScavengerAI self, Tracker.CreatureRepresentation subRep, Tracker.CreatureRepresentation objRep, bool objIsMe, PhysicalObject item)
        {
            orig(self, subRep, objRep, objIsMe, item);

            try
            {
                if (!isPlayer(subRep.representedCreature, out _))
                {
                    return;
                }
                if (!objIsMe && objRep != null && objRep.representedCreature.creatureTemplate.type != CreatureTemplate.Type.Scavenger)
                {
                    return;
                }
                if (EvolutionTree.TryGetEvolution(self.creature, item.abstractPhysicalObject, out var evolution))
                {
                    var evolutionTracker = ((ScavengerAbstractAI)self.creature.abstractAI).GetEvolutionTracker();
                    evolutionTracker.evolutionHelpers.Add(new WeakReference<AbstractPhysicalObject>(item.abstractPhysicalObject), subRep.representedCreature.ID);
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

    }
}
