using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using IL;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MoreSlugcats;
using On;
using RWCustom;
using SprobDesecratingGraves;
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

            On.PreyTracker.TrackedPrey.Attractiveness += ScavengerJunior_TrackedPrey_Attractiveness;
            On.ThreatTracker.Utility += ScavengerAI_ThreatTracker_Utility;
            On.ScavengerAI.IdleScore += ScavengerJunior_ScavengerAI_IdleScore;

            // punishment for being a nuisence
            On.PhysicalObject.Grabbed += ScavengerJunior_PhysicalObject_Grabbed;
            On.ScavengerAI.IUseARelationshipTracker_UpdateDynamicRelationship += ScavengerJunior_ScavengerAI_IUseARelationshipTracker_UpdateDynamicRelationship;
            IL.ScavengerAI.SocialEvent += ScavengerJunior_SocialEvent;
        }

        float ScavengerJunior_TrackedPrey_Attractiveness(On.PreyTracker.TrackedPrey.orig_Attractiveness orig, PreyTracker.TrackedPrey self)
        {
            float ret = orig(self);
            try
            {
                if (self.owner.AI is ScavengerAI scavAI)
                {
                    if (self.critRep?.representedCreature?.realizedCreature is Creature critter)
                    {
                        if (ScavengerJunior_CreatureHoldingKid(scavAI, critter) != CreatureHoldingJunior.NotHoldingKid)
                        {
                            return ret * 10f;
                        }
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            return ret;
        }


        float ScavengerAI_ThreatTracker_Utility(On.ThreatTracker.orig_Utility orig, ThreatTracker self)
        {
            try
            {
                if (self.AI is ScavengerAI scavAI)
                {
                    if (self.mostThreateningCreature?.representedCreature?.realizedCreature is Creature critter)
                    {
                        if (ScavengerJunior_CreatureHoldingKid(scavAI, critter) != CreatureHoldingJunior.NotHoldingKid)
                        {
                            return Mathf.Min(0.05f, orig(self));
                        }
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            return orig(self);
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

                if (obj is Scavenger scav && !scav.dead)
                {
                    if (scav.isJunior() && scav.abstractCreature.abstractAI.followCreature == self.creature)
                    {
                        if (ParentalParams.getOrAdd(scav.AI).wantCarryTimer > 0)
                        {
                            return 50;
                        }
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

        class AbstractBehaviorParams
        {
            static ConditionalWeakTable<ScavengerAbstractAI, AbstractBehaviorParams> map = new();
            public ScavengerAbstractAI owner;

            public bool toldToStay = false;
            AbstractBehaviorParams(ScavengerAbstractAI owner)
            {
                this.owner = owner;
                map.Add(owner, this);
            }

            static public AbstractBehaviorParams getOrAdd(ScavengerAbstractAI ai)
            {
                AbstractBehaviorParams ret;
                if (!map.TryGetValue(ai, out ret))
                {
                    ret = new AbstractBehaviorParams(ai);
                }
                return ret;
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
                        if (!state.currentParent.HasValue && ScavengerJunior_WantToHaveParent(self))
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
                        if (!ScavengerJunior_EvaluateGoodParent(self, self.followCreature) || !ScavengerJunior_WantToHaveParent(self))
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

                    AbstractBehaviorParams behaviorParams = AbstractBehaviorParams.getOrAdd(self);
                    if (self.parent.PacifiedBecauseCarried || ControlledScavenger(self.parent)) behaviorParams.toldToStay = false;
                    if (self.followCreature is null || !isPlayer(self.followCreature, out _)) behaviorParams.toldToStay = false;
                    if (behaviorParams.toldToStay)
                    {
                        self.freeze = 40;
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self, time);
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

                if (self is Scavenger scav && scav.isJunior() && scav.room is not null)
                {
                    if (JuniorOnBack.onback_map.TryGetValue(scav, out var onback)) onback.ChangeOverlap(true);
                    if (isPlayer(grasp.grabber.abstractCreature, out _) && !JuniorNuisanceImmunity)
                    {
                        if (scav.AI.threatTracker.Utility() > 0.1 && scav.AI.threatTracker.mostThreateningCreature.representedCreature != grasp.grabber.abstractCreature &&
                            Custom.DistLess(scav.room.MiddleOfTile(scav.AI.threatTracker.mostThreateningCreature.BestGuessForPosition()), scav.mainBodyChunk.pos, 400f))
                        {
                            if (ParentalParams.getOrAdd(scav.AI).dangerSaviorCounter <= 0)
                            {
                                Logger.LogDebug($"{grasp.grabber.abstractCreature} saved {scav.abstractCreature} from {scav.AI.threatTracker.mostThreateningCreature.representedCreature}");
                                ScavPlayerRelationChange(scav.AI, scav.AI.threatTracker.Utility() * 0.25f, grasp.grabber.abstractCreature); // Thank you!
                            }
                            ParentalParams.getOrAdd(scav.AI).dangerSaviorCounter = Mathf.Max(400, ParentalParams.getOrAdd(scav.AI).dangerSaviorCounter);
                        }
                        else if (!ScavengerJunior_EvaluateGoodParent((ScavengerAbstractAI)scav.abstractCreature.abstractAI, grasp.grabber.abstractCreature))
                        {
                            if (ParentalParams.getOrAdd(scav.AI).juniorNuisanceCooldown <= 0)
                            {
                                Logger.LogDebug($"{grasp.grabber.abstractCreature} is being a Nuisance to {scav.abstractCreature}");
                                self.room.socialEventRecognizer.SocialEvent(SESocialEvent.JuniorNuisance, grasp.grabber.abstractCreature.realizedCreature, scav, null);
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
                                violence_score = 2.0f;
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

                // 71	00A4	ldarg.1
                // 72	00A5	ldsfld	class SocialEventRecognizer/EventID SocialEventRecognizer/EventID::Theft
                // 73	00AA	call	bool class ExtEnum`1<class SocialEventRecognizer/EventID>::op_Equality(class ExtEnum`1<!0>, class ExtEnum`1<!0>)
                // 74	00AF	brfalse.s	84 (00C7) ldarg.1 
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdarg(1),
                    x => x.MatchLdsfld<SocialEventRecognizer.EventID>(nameof(SocialEventRecognizer.EventID.Theft)),
                    x => x.MatchCall(out _)
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_2); // subject crit
                cursor.Emit(OpCodes.Ldarg_3); // object crit
                cursor.EmitDelegate((bool istheft, ScavengerAI self, Creature subject_crit, Creature object_crit) =>
                {
                    if (istheft)
                    {
                        // if i'm the parent, don't consider me taking an items stealing.
                        if (object_crit == self.scavenger && self.scavenger.isJunior() && (self.creature.abstractAI.followCreature == subject_crit.abstractCreature))
                        {
                            istheft = false;
                        }
                    }
                    return istheft;
                });

            }
            catch (Exception except)
            {
                Logger.LogDebug(except);
            }
        }

        public float ScavengerJunior_ScavengerAI_IdleScore(On.ScavengerAI.orig_IdleScore orig, ScavengerAI self, WorldCoordinate tstPs)
        {
            float value = orig(self, tstPs);
            try
            {
                if (self.creature.abstractAI.followCreature is not null && self.scavenger.isJunior())
                {
                    var distance = Custom.BetweenRoomsDistance(self.creature.world, tstPs, self.creature.abstractAI.followCreature.pos);	
			        value -= Custom.LerpMap(distance, ScavengerParentTracker.desiredCloseness, ScavengerParentTracker.desiredCloseness*3f, 0f, 1000f);
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            return value;
        }

        public void ScavengerJunior_ScavengerAI_DecideBehavior(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((ScavengerAI self) =>
                {
                    if (self.creature.abstractAI.followCreature?.realizedCreature is Player p)
                    {
                        if (p.input[0].jmp && !p.input[1].jmp && p.bodyMode != Player.BodyModeIndex.Default)
                        {
                            if (p.input[0].y == -1 && p.input[0].x == 0)
                            {
                                AbstractBehaviorParams.getOrAdd((ScavengerAbstractAI)self.creature.abstractAI).toldToStay = true;
                            }
                        }
                    }

                     if (self.creature.abstractAI.followCreature?.realizedCreature is Scavenger scav && scav.inputWithDiagonals.HasValue && scav.lastInputWithDiagonals.HasValue)
                    {
                        if (scav.inputWithDiagonals.Value.jmp && !scav.lastInputWithDiagonals.Value.jmp)
                        {
                            if (scav.inputWithDiagonals.Value.y == -1 && scav.inputWithDiagonals.Value.x == 0)
                            {
                                AbstractBehaviorParams.getOrAdd((ScavengerAbstractAI)self.creature.abstractAI).toldToStay = true;
                            }
                        }
                    }

                    var parentalParams = ParentalParams.getOrAdd(self);
                    if (self.creature.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                    {
                        if (self.creature.abstractAI.followCreature is AbstractCreature parent)
                        {
                            if (parent.realizedCreature is Scavenger scavdad && ScangerJunior_WantToBeHeld(self, scavdad.AI))
                            {
                                parentalParams.wantCarryTimer = 200;

                                var held_juniors = scavdad.grasps.Where(x => x is not null && x.grabbed is Scavenger scav && scav.isJunior());
                                if (held_juniors.Count() < 1 && !parent.GetAllConnectedObjects().Contains(self.creature) && scavdad.AI.giftForMe == null 
                                        && scavdad.AI.pathFinder.CoordinateReachable(self.creature.pos) && !self.scavenger.grabbedBy.Any()
                                    )
                                {
                                    // Make scav dad into pick me up
                                    scavdad.AI.scavengeCandidate = scavdad.AI.itemTracker.RepresentationForObject(self.scavenger, AddIfMissing: true);
                                    parent.abstractAI.SetDestination(self.scavenger.room.GetWorldCoordinate(self.scavenger.firstChunk.pos));
                                }
                            }
                            else
                            {
                                parentalParams.wantCarryTimer -= 1;
                            }
                        }
                    }

                    parentalParams.juniorNuisanceCooldown -= 1;
                    parentalParams.parentalBloodlust -= 1;
                    parentalParams.dangerSaviorCounter -= 1;
                });

                /*
                    36	007E	ldarg.0
                    37	007F	call	instance class UtilityComparer ArtificialIntelligence::get_utilityComparer()
                    38	0084	callvirt	instance class AIModule UtilityComparer::HighestUtilityModule()
                    39	0089	stloc.0
                */
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchCall(typeof(ArtificialIntelligence).GetProperty(nameof(ArtificialIntelligence.utilityComparer)).GetGetMethod()),
                    x => x.MatchCallvirt<UtilityComparer>(nameof(UtilityComparer.HighestUtilityModule)),
                    x => x.MatchStloc(0)
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

                    var parentalParams = ParentalParams.getOrAdd(self);
                    if (self.focusCreature?.representedCreature?.realizedCreature is not null)
                    {
                        if (parentalParams.parentalBloodlust > 0)
                        {
                            self.utilityComparer.GetUtilityTracker(self.preyTracker).weight = Mathf.Min(self.utilityComparer.GetUtilityTracker(self.preyTracker).weight * 1.0f, 1f);
                            self.utilityComparer.GetUtilityTracker(self.threatTracker).weight = 0f;
                        }
                    }
                });


                // 36	007E	ldarg.0
                // 37	007F	call	instance class UtilityComparer ArtificialIntelligence::get_utilityComparer()
                // 38	0084	callvirt	instance class AIModule UtilityComparer::HighestUtilityModule()
                // 39	0089	stloc.0
                int utility_module_loc = 0;
                int utility_loc = 1;
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
                        self.runSpeedGoal = parentTracker.RunSpeed();
                        if (parentTracker.RunSpeed() < 0.1f)
                        {
                            self.behavior = ScavengerAI.Behavior.Idle;
                            self.idleCounter = 40;
                            self.testIdlePos = self.creature.pos;
                            self.creature.abstractAI.SetDestination(self.creature.pos);
                        }

                        if (parentTracker.abstractParent is not null && !parentTracker.abstractParent.slatedForDeletion)
                        {
                            self.focusCreature = self.tracker.RepresentationForCreature(parentTracker.abstractParent, true);
                        }
                    }
                    else
                    {
                        AbstractBehaviorParams.getOrAdd((ScavengerAbstractAI)self.creature.abstractAI).toldToStay = false;
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

        public void ScavengerJunior_ScavengerAI_Update(ILContext context)
        {
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
                    if (self.focusCreature?.representedCreature?.realizedCreature is Creature critter &&
                        ScavengerJunior_CreatureHoldingKid(self, critter) != CreatureHoldingJunior.NotHoldingKid &&
                        !self.creature.PacifiedBecauseCarried)
                    {
                        self.CheckThrow();
                    }

                    if (self.behavior == SEScavengerBehaviors.FollowParent)
                    {
                        if (ScavengerParentTracker.map.TryGetValue(self, out var tracker) && tracker.lastParentPos.HasValue)
                        {
                            self.runSpeedGoal = Mathf.Lerp(0f, 0.7f, tracker.RunSpeed() + self.threatTracker.Panic);
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

            public int juniorNuisanceCooldown = 0;
            public int parentalBloodlust = 0;
            public int wantCarryTimer = 0;
            public int dangerSaviorCounter = 0;
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
                        // lock in
                        self.agitation = Mathf.Max(self.agitation, (holdingkid == CreatureHoldingJunior.HoldingMYKid) ? 1.0f : 0.5f);
                        self.scared = Mathf.Min(self.agitation, (holdingkid == CreatureHoldingJunior.HoldingMYKid) ? 0f : 0.25f);
                        self.bloodLust = 25;

                        self.focusCreature = self.tracker.RepresentationForCreature(critter.abstractCreature, true);
                        relationship.type = CreatureTemplate.Relationship.Type.Attacks;
                        relationship.intensity = (holdingkid == CreatureHoldingJunior.HoldingMYKid) ? 1.0f : 0.8f;
                        if (dRelation.state is ScavengerAI.ScavengerTrackState state)
                        {
                            state.taggedViolenceType = ScavengerAI.ViolenceType.Lethal;
                        }

                        self.currentViolenceType = ScavengerAI.ViolenceType.Lethal;
                        if (holdingkid == CreatureHoldingJunior.HoldingMYKid)
                        {
                            ParentalParams.getOrAdd(self).parentalBloodlust += 300;
                        }
                    }

                    if (critter.abstractCreature == self.creature.abstractAI.followCreature)
                    {
                        if (relationship.type != CreatureTemplate.Relationship.Type.Pack) relationship.intensity = 0.5f;
                        relationship.type = CreatureTemplate.Relationship.Type.Pack;
                        relationship.intensity = Mathf.Max(relationship.intensity, 0.5f);
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
                    if (critter is Player && ParentalParams.getOrAdd(ai).dangerSaviorCounter > 0) continue; // attempting to save junior.
                    float appreciation = ScavengerJunior_AppreciateParent((ScavengerAbstractAI)scav.abstractAI, critter.abstractCreature);
                    if (appreciation > 0.1f) continue; // one of his friends?
                    return (scav.abstractAI.followCreature == ai.creature) ? CreatureHoldingJunior.HoldingMYKid : CreatureHoldingJunior.HoldingAKid;
                }
            }

            return CreatureHoldingJunior.NotHoldingKid;
        }


        public bool ScavengerJunior_CheckParent(ScavengerAbstractAI junior, AbstractCreature possibleParent)
        {
            if (!ScavengerJunior_WantToHaveParent(junior)) return false;
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

        public static AbstractCreature NotSlugcatPlayables_getPlayerController(AbstractCreature player)
        {
            return player.GetScavengerData()?.controller?.abstractCreature ?? player;
        }

        public static void ScavPlayerRelationChange(ScavengerAI self, float change, AbstractCreature player)
        {
            int playernum = -1;
            if (player.state is PlayerState pstate)
            {
                playernum = pstate.playerNumber;
            }
            else if (NotSlugcatPlayables && isNotSlugcatsPlayer(player, out playernum))
            {
                player = NotSlugcatPlayables_getPlayerController(player);
            }

            SocialMemory.Relationship orInitiateRelationship = self.creature.state.socialMemory.GetOrInitiateRelationship(player.ID);
            orInitiateRelationship.InfluenceTempLike(change * 1.5f);
            orInitiateRelationship.InfluenceLike(change * 0.5f);
            orInitiateRelationship.InfluenceKnow(Mathf.Abs(change));
            self.creature.world.game.session.creatureCommunities.InfluenceLikeOfPlayer(
                self.creature.creatureTemplate.communityID,
                self.creature.world.RegionNumber, playernum, change * 0.1f, 0.75f, 0.25f);
        }
        
        public bool ScavengerJunior_WantToHaveParent(ScavengerAbstractAI junior)
        {
            if (junior.parent.creatureTemplate.type != SECreatureEnums.ScavengerJunior) return false;
            if (ControlledScavenger(junior.parent)) return false;
            return true;
        }

        public bool ScavengerJunior_GetAdopted(ScavengerAbstractAI junior, AbstractCreature parent)
        {
            if (!ScavengerJunior_WantToHaveParent(junior))
            {
                Logger.LogError($"{parent} lost custody of {junior.parent} because they didn't want to be adopted.");
                return false;
            }
            ScavolutionPlugin.pubLogger?.LogDebug(new StackTrace());
            bool allowed_to_adopt = parent.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger;
            allowed_to_adopt = allowed_to_adopt || isPlayer(parent, out _);

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
                if (junior.RealAI is ScavengerAI scavai && state.currentParent != parent.ID.number && isPlayer(parent, out _))
                {
                    ScavPlayerRelationChange(scavai, 0.25f, parent);
                }
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
                if (junior.RealAI is ScavengerAI scavai && isPlayer(junior.followCreature, out _))
                {
                    ScavPlayerRelationChange(scavai, -0.25f, junior.followCreature);
                }

            }
            junior.followCreature = null;
            if (junior.parent.state is JuniorState state)
            {
                state.currentParent = null;
                state.cyclesSinceSeenParent = 0;
            }

            AbstractBehaviorParams.getOrAdd(junior).toldToStay = false;
        }


        public static bool ScavengerJunior_EvaluateGoodParent(ScavengerAbstractAI junior, AbstractCreature parent)
        {
            if (parent.state.dead) return false;
            float appreciation = ScavolutionPlugin.ScavengerJunior_AppreciateParent(junior, parent);
            return appreciation > 0.4;
        }

        public static bool isPlayer(AbstractCreature creature, out int playerNumber)
        {
            playerNumber = -1;
            if (creature.creatureTemplate.type == CreatureTemplate.Type.Slugcat && creature.state is PlayerState pstate)
            {
                playerNumber = pstate.playerNumber;
                return true;
            }

            if (NotSlugcatPlayables)
            {
                if (isNotSlugcatsPlayer(creature, out playerNumber)) return true;
            }

            return false;
        }

        public static bool isNotSlugcatsPlayer(AbstractCreature creature, out int playerNumber)
        {
            playerNumber = -1;
            if (creature.creatureTemplate.type == SprobDesecratingGraves.Enums.CreatureTemplateType.PlayerScavenger)
            {
                var controller = SprobDesecratingGraves.ScavengerHooks.GetScavengerData(creature).controller;
                if (controller is not null)
                {
                    playerNumber = controller.playerState.playerNumber;
                    return true;
                }
            }

            return false;
        }

        public static float ScavengerJunior_AppreciateParent(ScavengerAbstractAI junior, AbstractCreature parent)
        {
            if (isPlayer(parent, out int playerNum))
            {
                var actualParent = parent;
                if (NotSlugcatPlayables)
                {
                    actualParent = NotSlugcatPlayables_getPlayerController(parent);
                }

                float relationship = junior.parent.state.socialMemory.GetOrInitiateRelationship(actualParent.ID).like;
                float reputation = junior.parent.world.game.session.creatureCommunities.LikeOfPlayer(
                    CreatureCommunities.CommunityID.Scavengers, junior.parent.world.RegionNumber, playerNum
                );

                if (reputation > 0.9 && relationship >= 0f)
                {
                    relationship = Math.Max(0.5f, relationship);
                }

                return relationship - Mathf.Min(relationship, 0.4f);
            }


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

            return 0.0f;
        }

        class ScavengerParentTracker : AIModule
        {
            public static ConditionalWeakTable<ArtificialIntelligence, ScavengerParentTracker> map = new();
            public ScavengerParentTracker(ArtificialIntelligence intel) : base(intel)
            {
                map.Add(intel, this);
            }

            public const float desiredCloseness = 6f;
            public float Urgency
            {
                get
                {
                    if (abstractParent is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return 0.0f;
                    return 0.8f;
                }
            }

            public float DynamicDesiredCloseness()
            {
                if (abstractParent is null || abstractParent.Room is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return desiredCloseness;
                if (abstractParent.Room.gate || abstractParent.Room.shelter)
                {
                    return 3f;
                }
                return desiredCloseness;
            }

            public override float Utility()
            {
                if (AbstractBehaviorParams.getOrAdd((ScavengerAbstractAI)AI.creature.abstractAI).toldToStay)
                {
                    return 0.5f;
                }

                if (abstractParent is null || abstractParent.Room is null || abstractParent.state.dead || abstractParent.slatedForDeletion) return 0f;
                if (abstractParent.pos.room != AI.creature.pos.room)
                {
                    return Urgency;
                }

                if (abstractParent.Room.gate || abstractParent.Room.shelter)
                {
                    return 1f;
                }

                var dist = abstractParent.pos.Tile.FloatDist(AI.creature.pos.Tile);
                if (dist < DynamicDesiredCloseness())
                {
                    return 0f;
                }
                    
                return Custom.LerpMap(dist, DynamicDesiredCloseness(), DynamicDesiredCloseness() * 3f, 0.2f, 1f) * Urgency; ;
            }

            public float RunSpeed()
            {
                if (AbstractBehaviorParams.getOrAdd((ScavengerAbstractAI)AI.creature.abstractAI).toldToStay)
                {
                    return 0f;
                }


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

                if (abstractParent.pos.room != lastParentPos.Value.room || abstractParent.pos.Tile.FloatDist(lastParentPos.Value.Tile) > DynamicDesiredCloseness())
                {
                    parentMovingCounter = 100;
                    lastParentPos = abstractParent.pos;
                }
                else if (parentMovingCounter > 0)
                {
                    parentMovingCounter--;
                }
            }

            public AbstractCreature? abstractParent => this.AI.creature.abstractAI.followCreature;
            public WorldCoordinate? lastParentPos;
            public int parentMovingCounter = 0;
        }


    }
}