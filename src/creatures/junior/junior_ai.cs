using System;
using System.Linq;
using System.Runtime.CompilerServices;
using IL;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using On;
using RWCustom;
using UnityEngine;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void JuniorAIHooks()
        {
            On.ScavengerAI.WeaponScore += ScavengerAI_WeaponScoreJunior;
            On.ScavengerAI.CollectScore_PhysicalObject_bool += ScavengerAI_CollectScoreJunior;

            // parentship
            On.ScavengersWorldAI.AddScavenger += ScavengerJunior_ScavengerWorldAI_AddScavenger;
            On.ScavengerAbstractAI.AbstractBehavior += ScavengerJunior_ScavengerAbstractAI_AbstractBehavior;
            On.ScavengerAI.ctor += ScavengerJunior_ScavengerAI_ctor;
            On.ScavengerAI.CreatureSpotted += ScavengerJunior_ScavengerAI_CreatureSpotted;
            IL.ScavengerAI.DecideBehavior += ScavengerJunior_ScavengerAI_DecideBehavior;
            IL.ScavengerAI.Update += ScavengerJunior_ScavengerAI_Update;

            On.PhysicalObject.Grabbed += ScavengerJunior_PhysicalObject_Grabbed;
            On.ScavengerAI.IUseARelationshipTracker_UpdateDynamicRelationship += ScavengerJunior_ScavengerAI_IUseARelationshipTracker_UpdateDynamicRelationship;
            IL.ScavengerAI.SocialEvent += ScavengerJunior_SocialEvent;
        }

        int ScavengerAI_WeaponScoreJunior(On.ScavengerAI.orig_WeaponScore orig, ScavengerAI self, PhysicalObject obj, bool pickupDropInsteadOfWeaponSelection, bool reallyWantsSpear)
        {
            try
            {
                if (self.scavenger.isJunior())
                {
                    reallyWantsSpear = false;
                    if (obj is Spear)
                    {
                        return 0; // garbage spear throw
                    }

                    if (obj is Boomerang || obj is Rock)
                    {
                        return 10;
                    }

                    if (obj is ScavengerBomb)
                    {
                        return 8;
                    }
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            return orig(self, obj, pickupDropInsteadOfWeaponSelection, reallyWantsSpear);
        }

        int ScavengerAI_CollectScoreJunior(On.ScavengerAI.orig_CollectScore_PhysicalObject_bool orig, ScavengerAI self, PhysicalObject obj, bool weaponFiltered)
        {

            try
            {

                if (self.scavenger.isJunior())
                {
                    if (obj is Rock)
                    {
                        return 4;
                    }

                    if (obj is ScavengerBomb)
                    {
                        return 6;
                    }

                    if (ModManager.Watcher && obj is Boomerang)
                    {
                        return 5;
                    }

                    if (obj is DataPearl)
                    {
                        return 2; // scav juniors don't value datapearls as much
                    }

                    if (obj is Spear)
                    {
                        return 0;
                    }

                    if (obj is JellyFish)
                    {
                        return 7;
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }


            return orig(self, obj, weaponFiltered);
        }

        public void ScavengerJunior_ScavengerAI_ctor(On.ScavengerAI.orig_ctor orig, ScavengerAI self, AbstractCreature creature, World world)
        {
            orig(self, creature, world);
            try
            {
                if (creature.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    var parentTracker = new ScavengerParentTracker(self);
                    self.AddModule(parentTracker);
                    self.utilityComparer.AddComparedModule(parentTracker, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.033333335f), 0.5f, 0.8f);
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }


        public void ScavengerJunior_ScavengerWorldAI_AddScavenger(On.ScavengersWorldAI.orig_AddScavenger orig, ScavengersWorldAI self, ScavengerAbstractAI newScav)
        {
            orig(self, newScav);
            try
            {
                if (newScav.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    for (int i = 0; i < self.scavengers.Count; i++)
                    {
                        if (self.scavengers[i].parent == newScav.parent) continue;
                        ScavengerJunior_CheckParent(newScav, self.scavengers[i].parent);
                    }
                }
                else
                {
                    for (int i = 0; i < self.scavengers.Count; i++)
                    {
                        if (self.scavengers[i].parent.creatureTemplate.type != SECreatureEnums.ScavengerJunior) continue;
                        if (self.scavengers[i].parent == newScav.parent) continue;
                        ScavengerJunior_CheckParent(self.scavengers[i], newScav.parent);
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        public void ScavengerJunior_ScavengerAbstractAI_AbstractBehavior(On.ScavengerAbstractAI.orig_AbstractBehavior orig, ScavengerAbstractAI self, int time)
        {
            orig(self, time);
            try
            {
                if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    if (self.parent.state is JuniorState state && state.alive)
                    {
                        if (!state.currentParent.HasValue)
                        {
                            float current_appreciation = 0.2f;
                            AbstractCreature? bestScav = null;
                            for (int i = 0; i < self.worldAI.scavengers.Count; i++)
                            {
                                if (self.worldAI.scavengers[i].parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior) continue;
                                float appreciation = ScavengerJunior_AppreciateParent(self, self.worldAI.scavengers[i].parent);
                                if (appreciation > current_appreciation)
                                {
                                    current_appreciation = appreciation;
                                    bestScav = self.worldAI.scavengers[i].parent;
                                }
                            }

                            if (bestScav != null)
                            {
                                ScavengerJunior_GetAdopted(self, bestScav);
                            }
                        }
                    }

                    if (self.followCreature is not null)
                    {
                        if (!ScavengerJunior_EvaluateGoodParent(self, self.followCreature))
                        {
                            ScavengerJunior_LoseCustody(self);
                        }
                        else
                        {
                            if (self.squad == null)
                            {
                                if (!self.GoHome()) self.GoToRoom(self.followCreature.pos.room);
                                if (self.followCreature.abstractAI is ScavengerAbstractAI parentAI && parentAI.squad != null)
                                {
                                    parentAI.squad.AddMember(self.parent);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void ScavengerJunior_ScavengerAI_CreatureSpotted(On.ScavengerAI.orig_CreatureSpotted orig, ScavengerAI self, bool firstSpot, Tracker.CreatureRepresentation rep)
        {
            orig(self, firstSpot, rep);
            try
            {
                if (self.creature.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    if (self.creature != rep.representedCreature)
                    {
                        ScavengerJunior_CheckParent((ScavengerAbstractAI)self.creature.abstractAI, rep.representedCreature);
                    }
                    
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }


        public static bool JuniorNuisanceImmunity = false;
        public void ScavengerJunior_PhysicalObject_Grabbed(On.PhysicalObject.orig_Grabbed orig, PhysicalObject self, Creature.Grasp grasp)
        {
            orig(self, grasp);
            try
            {
                if (grasp.grabber is Player p && !JuniorNuisanceImmunity)
                {
                    if (self is Scavenger scav && scav.isJunior() && scav.room is not null)
                    {
                        if (!ScavengerJunior_EvaluateGoodParent((ScavengerAbstractAI)scav.abstractCreature.abstractAI, grasp.grabber.abstractCreature))
                        {
                            if (ParentalParams.getOrAdd(scav.AI).juniorNuisanceCooldown <= 0)
                            {
                                Logger.LogDebug($"{p.abstractCreature} is being a Nuisance to {scav.abstractCreature}");
                                self.room.socialEventRecognizer.SocialEvent(SESocialEvent.JuniorNuisance, p, scav, null);
                                ParentalParams.getOrAdd(scav.AI).juniorNuisanceCooldown = 160;
                            }
                        }
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogDebug(except);
            }

            
        }

        public void ScavengerJunior_SocialEvent(ILContext context)
        {
            try
            {
                /*
                    69	009E	ldc.r4	0
                    70	00A3	stloc.3
                */
                int social_effect_loc = 3;
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.After,
                    x => x.Match(OpCodes.Ldc_R4),
                    x => x.MatchStloc(social_effect_loc));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_1); // Social Event ID
                cursor.Emit(OpCodes.Ldloc, social_effect_loc);
                cursor.Emit(OpCodes.Ldarg_2); // subject crit
                cursor.Emit(OpCodes.Ldarg_3); // object crit
                cursor.EmitDelegate((ScavengerAI self, SocialEventRecognizer.EventID ID, float violence_score, Creature subject_crit, Creature object_crit) =>
                {

                    try
                    {
                        if (object_crit is Scavenger scav && scav.isJunior())
                        {
                            if (ID == SESocialEvent.JuniorNuisance)
                            {
                                violence_score = 1.0f;
                                if (object_crit.abstractCreature?.abstractAI?.followCreature == self.creature)
                                {
                                    violence_score *= 2f;
                                }
                            }
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.LogError(except);
                    }

                    return violence_score;
                });
                cursor.Emit(OpCodes.Stloc, social_effect_loc);


            }
            catch (Exception except)
            {
                Logger.LogDebug(except);
            }        
        }

        public void ScavengerJunior_ScavengerAI_DecideBehavior(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                int utility_module_loc = 0;
                int utility_loc = 1;
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchCall(typeof(ScavengerAI).GetProperty(nameof(ScavengerAI.utilityComparer)).GetGetMethod()),
                    x => x.MatchCallvirt<UtilityComparer>(nameof(UtilityComparer.HighestUtilityModule)),
                    x => x.MatchStloc(out utility_module_loc)
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((ScavengerAI self) =>
                {
                    if (ScavengerParentTracker.map.TryGetValue(self, out var parentTracker))
                    {
                        self.utilityComparer.GetUtilityTracker(self.rainTracker).weight = parentTracker.CareAboutRain() ? 1.0f : 0.1f;
                    }
                    else
                    {
                        self.utilityComparer.GetUtilityTracker(self.rainTracker).weight = 1.0f;
                    }

                    if (self.focusCreature?.representedCreature?.realizedCreature is not null)
                    {
                        if (ParentalParams.getOrAdd(self).parentalBloodlust > 0)
                        {
                            self.utilityComparer.GetUtilityTracker(self.preyTracker).weight = 1.0f;
                        }
                    }

                    ParentalParams.getOrAdd(self).juniorNuisanceCooldown -= 1;
                    ParentalParams.getOrAdd(self).parentalBloodlust -= 1;
                });


                // 36	007E	ldarg.0
                // 37	007F	call	instance class UtilityComparer ArtificialIntelligence::get_utilityComparer()
                // 38	0084	callvirt	instance class AIModule UtilityComparer::HighestUtilityModule()
                // 39	0089	stloc.0
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCall(typeof(ScavengerAI).GetProperty(nameof(ScavengerAI.utilityComparer)).GetGetMethod()),
                    x => x.MatchCallvirt<UtilityComparer>(nameof(UtilityComparer.HighestUtilityModule)),
                    x => x.MatchStloc(out utility_module_loc)
                );

                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<ScavengerAI>(nameof(ScavengerAI.currentUtility)),
                    x => x.MatchStloc(out utility_loc)
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, utility_module_loc);
                cursor.Emit(OpCodes.Ldloc, utility_loc);
                cursor.EmitDelegate((ScavengerAI self, AIModule module, float utility) =>
                {
                    if (self.creature.abstractAI.followCreature is not null)
                    {
                        if (!ScavengerJunior_EvaluateGoodParent((ScavengerAbstractAI)self.creature.abstractAI, self.creature.abstractAI.followCreature))
                        {
                            ScavengerJunior_LoseCustody((ScavengerAbstractAI)self.creature.abstractAI);
                        }
                    }

                    if (module is ScavengerParentTracker parentTracker)
                    {
                        self.behavior = SEScavengerBehaviors.FollowParent;
                        if (parentTracker.abstractParent is not null && !parentTracker.abstractParent.slatedForDeletion)
                        {
                            self.focusCreature = self.tracker.RepresentationForCreature(parentTracker.abstractParent, true);
                        }
                    }
                });

                // 45	009B	ldarg.0
                // 46	009C	ldfld	float32 ScavengerAI::currentUtility
                // 47	00A1	stloc.1

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void ScavengerJunior_ScavengerAI_Update(ILContext context) {
            try
            {
                ILCursor cursor = new(context);
                // 36	007E	ldarg.0
                // 37	007F	call	instance class UtilityComparer ArtificialIntelligence::get_utilityComparer()
                // 38	0084	callvirt	instance class AIModule UtilityComparer::HighestUtilityModule()
                // 39	0089	stloc.0
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCall<ScavengerAI>(nameof(ScavengerAI.UpdateCurrentViolenceType))
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((ScavengerAI self) =>
                {
                    if (self.behavior == SEScavengerBehaviors.FollowParent)
                    {
                        if (ScavengerParentTracker.map.TryGetValue(self, out var tracker) && tracker.lastParentPos.HasValue)
                        {
                            self.runSpeedGoal = Mathf.Lerp(0f, 0.7f, tracker.RunSpeed());
                            self.creature.abstractAI.SetDestination(tracker.lastParentPos.Value);
                        }
                    }
                });


                // 45	009B	ldarg.0
                // 46	009C	ldfld	float32 ScavengerAI::currentUtility
                // 47	00A1	stloc.1

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        class ParentalParams
        {
            static ConditionalWeakTable<ScavengerAI, ParentalParams> map = new();
            public ScavengerAI owner;

            public int juniorNuisanceCooldown;
            public int parentalBloodlust = 0;
            ParentalParams(ScavengerAI owner)
            {
                this.owner = owner;
                map.Add(owner, this);
            }

            static public ParentalParams getOrAdd(ScavengerAI ai)
            {
                ParentalParams ret;
                if (!map.TryGetValue(ai, out ret))
                {
                    ret = new ParentalParams(ai);
                }
                return ret;
            }
        }
        

        

        public CreatureTemplate.Relationship ScavengerJunior_ScavengerAI_IUseARelationshipTracker_UpdateDynamicRelationship(On.ScavengerAI.orig_IUseARelationshipTracker_UpdateDynamicRelationship orig, global::ScavengerAI self, global::RelationshipTracker.DynamicRelationship dRelation)
        {
            CreatureTemplate.Relationship relationship = orig(self, dRelation);
            try
            {
                if (dRelation.trackerRep?.representedCreature?.realizedCreature is Creature critter)
                {
                    var holdingkid = ScavengerJunior_CreatureHoldingKid(self, critter);
                    if (holdingkid != CreatureHoldingJunior.NotHoldingKid)
                    {
                        self.agitation = Mathf.Max(self.agitation, (holdingkid == CreatureHoldingJunior.HoldingMYKid)? 1.0f : 0.5f);
                        self.focusCreature = self.tracker.RepresentationForCreature(critter.abstractCreature, true);
                        relationship.type = CreatureTemplate.Relationship.Type.Attacks;
                        relationship.intensity = (holdingkid == CreatureHoldingJunior.HoldingMYKid)? 1.0f : 0.5f;
                        if (dRelation.state is ScavengerAI.ScavengerTrackState state)
                        {
                            state.taggedViolenceType = (holdingkid == CreatureHoldingJunior.HoldingMYKid)? ScavengerAI.ViolenceType.Lethal : ScavengerAI.ViolenceType.Warning;
                        }

                        if (holdingkid == CreatureHoldingJunior.HoldingMYKid)
                        {
                            ParentalParams.getOrAdd(self).parentalBloodlust += 300;
                        }
                        
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            return relationship;
        }


        public enum CreatureHoldingJunior
        {
            NotHoldingKid = 0,
            HoldingAKid,
            HoldingMYKid
        }
        public CreatureHoldingJunior ScavengerJunior_CreatureHoldingKid(ScavengerAI ai, Creature critter)
        {
            if (critter.grasps is null) return CreatureHoldingJunior.NotHoldingKid;
            foreach (AbstractPhysicalObject.AbstractObjectStick stick in critter.abstractCreature.stuckObjects)
            {
                if (stick is null) continue;
                bool grabbing_stick = stick is AbstractPhysicalObject.CreatureGripStick;
                grabbing_stick = grabbing_stick || stick is JuniorOnBack.AbstractJuniorOnBackStick;
                if (!grabbing_stick) continue;

                if (stick.B is AbstractCreature scav && scav.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    if (critter is Scavenger) continue; // one of my friends
                    float appreciation = ScavengerJunior_AppreciateParent((ScavengerAbstractAI)scav.abstractAI, critter.abstractCreature);
                    if (appreciation > 0.1f) continue; // one of his friends?
                    return (scav.abstractAI.followCreature == ai.creature)? CreatureHoldingJunior.HoldingMYKid : CreatureHoldingJunior.HoldingAKid;
                }
            }

            return CreatureHoldingJunior.NotHoldingKid;
        }


        public bool ScavengerJunior_CheckParent(ScavengerAbstractAI junior, AbstractCreature possibleParent)
        {
            if (junior.followCreature is not null) return false;
            if (junior.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                if (junior.parent.state is JuniorState state && state.alive)
                {
                    if (state.currentParent.HasValue)
                    {
                        if (state.currentParent == possibleParent.ID.number)
                        {
                            return ScavengerJunior_GetAdopted(junior, possibleParent);
                        }
                    }
                }
            }

            return false;
        }

        public bool ScavengerJunior_GetAdopted(ScavengerAbstractAI junior, AbstractCreature parent)
        {

            bool allowed_to_adopt = parent.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger;
            allowed_to_adopt = allowed_to_adopt || parent.creatureTemplate.type == CreatureTemplate.Type.Slugcat;
            
            if (parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                allowed_to_adopt = false;
            }
            
            if (ModManager.MSC && parent.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing)
            {
                allowed_to_adopt = false;
            }
            
            

            if (!allowed_to_adopt)
            {
                Logger.LogError($"{parent} lost custody of {junior.parent} because they weren't the right creature type ({parent.creatureTemplate.type})!");
                return false;
            }


            if (!ScavengerJunior_EvaluateGoodParent(junior, parent))
            {
                Logger?.LogDebug($"{parent} failed to adopt {junior} because they don't like them");
                return false;
            }


            if (junior.parent.ID.number == parent.ID.number)
            {
                Logger.LogError($"{parent} lost custody of {junior.parent} because they were the same creature?");
                return false;
            }

            junior.followCreature = parent;
            if (junior.parent.state is JuniorState state)
            {
                state.currentParent = parent.ID.number;
                state.cyclesSinceSeenParent = 0;
            }

            Logger.LogDebug($"{parent} just adopted {junior.parent}!");
            return true;
        }

        public static void ScavengerJunior_LoseCustody(ScavengerAbstractAI junior)
        {
            if (junior.followCreature is not null)
            {
                ScavolutionPlugin.pubLogger?.LogDebug($"{junior.followCreature} lost custody of {junior.parent} ");
            }
            junior.followCreature = null;
            if (junior.parent.state is JuniorState state)
            {
                state.currentParent = null;
                state.cyclesSinceSeenParent = 0;
            }
        }


        public static bool ScavengerJunior_EvaluateGoodParent(ScavengerAbstractAI junior, AbstractCreature parent)
        {
            if (parent.state.dead) return false;
            float appreciation = ScavolutionPlugin.ScavengerJunior_AppreciateParent(junior, parent);
            return appreciation > 0.4;
        }


        public static float ScavengerJunior_AppreciateParent(ScavengerAbstractAI junior, AbstractCreature parent)
        {
            if (parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                return 0.0f;
            }

            if (ModManager.DLCShared)
            {
                if (parent.creatureTemplate.type == DLCSharedEnums.CreatureTemplateType.ScavengerElite)
                {
                    return 0.8f;
                }
            }

            if (ModManager.Watcher)
            {
                if (parent.creatureTemplate.type == Watcher.WatcherEnums.CreatureTemplateType.ScavengerTemplar)
                {
                    return 0.6f;
                }

                if (parent.creatureTemplate.type == Watcher.WatcherEnums.CreatureTemplateType.ScavengerDisciple)
                {
                    return 0.8f;
                }
            }

            if (ModManager.MSC)
            {
                if (parent.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.ScavengerKing)
                {
                    return 0f;
                }
            }

            if (parent.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger)
            {
                return 0.5f;
            }

            if (parent.creatureTemplate.type == CreatureTemplate.Type.Slugcat)
            {
                float relationship = junior.parent.state.socialMemory.GetOrInitiateRelationship(parent.ID).like;
                return relationship - Mathf.Min(relationship, 0.4f);
            }

            return 0.0f;
        }

        class ScavengerParentTracker : AIModule
        {
            public static ConditionalWeakTable<ArtificialIntelligence, ScavengerParentTracker> map = new();
            public ScavengerParentTracker(ArtificialIntelligence intel) : base(intel)
            {
                map.Add(intel, this);
            }

            const float desiredCloseness = 10f;
            public float Urgency
            {
                get
                {
                    if (abstractParent is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return 0.0f;
                    return ScavengerJunior_AppreciateParent((ScavengerAbstractAI)this.AI.creature.abstractAI, abstractParent);
                }
            }

            public override float Utility()
            {
                if (abstractParent is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return 0f;
                if (abstractParent.pos.room != AI.creature.pos.room)
                {
                    return Urgency;
                }

                if (abstractParent.Room.gate || abstractParent.Room.shelter)
                {
                    return 1f;
                }

                return Custom.LerpMap(abstractParent.pos.Tile.FloatDist(AI.creature.pos.Tile), desiredCloseness, desiredCloseness * 3f, 0.2f, 1f) * Urgency;
            }

            public float RunSpeed()
            {
                if (abstractParent is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return 0f;
                WorldCoordinate friendDest = abstractParent.pos;

                if (AI.creature.pos.room == friendDest.room)
                {
                    if (AI.creature.Room.shelter || AI.creature.Room.gate)
                    {
                        if (!(AI.creature.pos.Tile.FloatDist(friendDest.Tile) < 2f))
                        {
                            return 1f;
                        }

                        return 0f;
                    }

                    if (AI.creature.pos.Tile.FloatDist(friendDest.Tile) < 3f)
                    {
                        return 0f;
                    }
                }

                return Custom.LerpMap(AI.creature.pos.Tile.FloatDist(friendDest.Tile), 3f, 25f, 0.25f + Mathf.Min(AI.rainTracker.Utility(), 0.75f), 1f, (parentMovingCounter > 0) ? 0.5f : 1f);
            }

            public bool CareAboutRain()
            {
                if (abstractParent is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return true;
                if (abstractParent.pos.room == AI.creature.pos.room)
                {
                    return false;
                }

                for (int i = 0; i < abstractParent.Room.connections.Length; i++)
                {
                    if (abstractParent.Room.connections[i] == AI.creature.pos.room)
                    {
                        return false;
                    }
                }

                return true;
            }

            public override void Update()
            {
                if (abstractParent is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return;
                if (!lastParentPos.HasValue)
                {
                    parentMovingCounter = 100;
                    lastParentPos = abstractParent.pos;
                }

                if (abstractParent.pos.room != lastParentPos.Value.room || abstractParent.pos.Tile.FloatDist(lastParentPos.Value.Tile) > desiredCloseness)
                {
                    parentMovingCounter = 100;
                    lastParentPos = abstractParent.pos;
                }
                else if (parentMovingCounter > 0)
                {
                    parentMovingCounter--;
                }

                if (parentMovingCounter > 0)
                {
                    tiredness++;
                }
                else
                {
                    --tiredness;
                    tiredness = Mathf.Max(tiredness, 0);
                }
            }

            public AbstractCreature? abstractParent => this.AI.creature.abstractAI.followCreature;
            public WorldCoordinate? lastParentPos;
            public int parentMovingCounter = 0;
            public int tiredness = 0;
        }


    }
}