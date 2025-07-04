using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace Scavolution
{
    public class JuniorOnBack
    {
        static public ConditionalWeakTable<Creature, JuniorOnBack> creature_map = new();
        static public ConditionalWeakTable<Scavenger, JuniorOnBack> onback_map = new();
        public Creature owner;
        public Scavenger? scavenger;
        public bool increment;
        public int counter;
        public bool interactionLocked;
        public class AbstractJuniorOnBackStick : AbstractPhysicalObject.AbstractObjectStick
        {
            public AbstractJuniorOnBackStick(AbstractPhysicalObject A, AbstractPhysicalObject B) : base(A, B) { }
        };

        public AbstractJuniorOnBackStick? stick = null;
        public JuniorOnBack(Creature p)
        {
            this.owner = p;
            creature_map.Add(p, this);
        }

        public void Update()
        {
            if (this.increment)
            {
                var backpacktime = this.owner is Player ? 20 : 100;

                this.counter++;
                if (this.counter > backpacktime)
                {
                    if (this.scavenger != null)
                    {
                        this.ScavtoHand();
                        this.counter = 0;
                    }
                    else if (this.scavenger == null)
                    {
                        for (int i = 0; i < this.owner.grasps.Length; i++)
                        {
                            if (this.owner.grasps[i] != null && this.owner.grasps[i].grabbed is Scavenger scav && scav.isJunior())
                            {
                                this.owner.bodyChunks[0].pos += Custom.DirVec(this.owner.grasps[i].grabbed.firstChunk.pos, this.owner.bodyChunks[0].pos) * 2f;
                                this.ScavtoBack(scav);
                                this.counter = 0;
                                break;
                            }
                        }
                    }
                }

                if (this.owner is Player p && p.isNPC)
                {
                    ChangeOverlap(true);
                }

                if (ModManager.DLCShared)
                {
                    if (scavenger != null)
                    {
                        if (scavenger.animation?.id == DLCSharedEnums.ScavengerAnimationID.Jumping)
                        {
                            ChangeOverlap(true);
                        }
                        else if (!ScavolutionPlugin.ScavengerJunior_EvaluateGoodParent((ScavengerAbstractAI)scavenger.abstractCreature.abstractAI, owner.abstractCreature))
                        {
                            if (scavenger.animation?.id != DLCSharedEnums.ScavengerAnimationID.PrepareToJump)
                            {
                                ScavolutionPlugin.pubLogger?.LogDebug("Attempting to jump off back");
                                Scavenger.JumpFinder jumpFinder = new Scavenger.JumpFinder(scavenger.room, scavenger, scavenger.abstractCreature.pos.Tile);
                                bool direction = false;
                                if (owner is Player p2) {
                                    direction = p2.flipDirection > 0f;
                                }
                                
                                jumpFinder.bestJump = new Scavenger.JumpFinder.JumpInstruction(scavenger.mainBodyChunk.pos, new Vector2(direction ? (-11.5f) : 11.5f, 13.5f), 0.5f + UnityEngine.Random.Range(-0.1f, 0.1f));
                                PathFinder.PathingCell goalCell = scavenger.AI.pathFinder.PathingCellAtWorldCoordinate(scavenger.abstractCreature.pos + new IntVector2(direction? -10 : 10, 0));
                                jumpFinder.bestJump.goalCell = goalCell;
                                scavenger.jumpFinders.Clear();
                                scavenger.jumpFinders.Add(jumpFinder);
                                scavenger.InitiateJump(jumpFinder, 30);
                            }
                        }
                    }
                }

                
                
            }
            else
            {
                this.counter = 0;
            }

            this.increment = false;
        }

        public void GraphicsModuleUpdated(bool actuallyViewed, bool eu)
        {
            if (scavenger == null)
            {
                return;
            }

            if (owner.slatedForDeletetion || scavenger.slatedForDeletetion || scavenger.grabbedBy.Count > 0 || !scavenger.Consious || !owner.Consious)
            {
                ChangeOverlap(true);
                return;
            }

            ChangeOverlap(newOverlap: false);

            if (ModManager.DLCShared)
            {
                if (scavenger.animation?.id == DLCSharedEnums.ScavengerAnimationID.Jumping)
                {
                    ChangeOverlap(true);
                    return;
                }
            }
            
            
            if (owner is Player p)
            {
                var restpos = (owner.graphicsModule is PlayerGraphics playerGraphics) ? playerGraphics.head.pos : owner.mainBodyChunk.pos;
                restpos += new Vector2(0, 14f);

                scavenger.flip = Mathf.Lerp(scavenger.flip, p.flipDirection, 0.8f);

                var offset = restpos - scavenger.bodyChunks[0].pos;
                scavenger.bodyChunks[0].RelativeMoveFromOutsideMyUpdate(eu, offset);
                scavenger.bodyChunks[1].RelativeMoveFromOutsideMyUpdate(eu, offset);
                scavenger.bodyChunks[2].RelativeMoveFromOutsideMyUpdate(eu, offset);

                if (scavenger.animation?.id != DLCSharedEnums.ScavengerAnimationID.PrepareToJump)
                {
                    scavenger.bodyChunks[0].vel = owner.mainBodyChunk.vel; // torsoe
                    scavenger.bodyChunks[1].vel = Vector2.Lerp(scavenger.bodyChunks[1].vel, owner.mainBodyChunk.vel, 0.5f); // legs
                }

                // no vel sync for head
            }

            if (owner is Scavenger scav_holder)
            {
                Vector2 headpos = scav_holder.mainBodyChunk.pos;
                headpos += new Vector2(-scav_holder.flip * 5f, 23f);

                scavenger.flip = Mathf.Lerp(scavenger.flip, scav_holder.flip, 0.8f);

                var offset = headpos - scavenger.bodyChunks[0].pos;
                scavenger.bodyChunks[0].RelativeMoveFromOutsideMyUpdate(eu, offset);
                scavenger.bodyChunks[1].RelativeMoveFromOutsideMyUpdate(eu, offset);
                scavenger.bodyChunks[2].RelativeMoveFromOutsideMyUpdate(eu, offset);

                if (!ModManager.DLCShared || (scavenger.animation?.id != DLCSharedEnums.ScavengerAnimationID.PrepareToJump))
                {
                    scavenger.bodyChunks[0].vel = scav_holder.bodyChunks[2].vel; // torsoe
                    scavenger.bodyChunks[1].vel = Vector2.Lerp(scavenger.bodyChunks[1].vel, scav_holder.bodyChunks[2].vel, 0.5f); // legsPrepareToJump
                }
            }


            if (!ModManager.DLCShared || ((scavenger.animation?.id != DLCSharedEnums.ScavengerAnimationID.PrepareToJump) && (scavenger.animation?.id != DLCSharedEnums.ScavengerAnimationID.Jumping)))
            {
                scavenger.movMode = Scavenger.MovementMode.StandStill;
                scavenger.moveModeChangeCounter = 0;
            }
        }
        public void ScavtoHand()
        {
            if (scavenger == null) return;

            if (this.owner is Player p)
            {
                if (p.FreeHand() is int a && a != -1)
                {
                    var oldimmunity = ScavolutionPlugin.JuniorNuisanceImmunity;
                    try
                    {
                        ScavolutionPlugin.JuniorNuisanceImmunity = true;
                        p.SlugcatGrab(scavenger, a);
                    }
                    finally
                    {
                        ScavolutionPlugin.JuniorNuisanceImmunity = oldimmunity;
                    }

                }
            }

            if (this.owner is Scavenger scav_holder)
            {
                scav_holder.PickUpAndPlaceInInventory(scavenger, true);
            }

            ChangeOverlap(true);
        }

        public void ScavtoBack(Scavenger scav)
        {
            if (scav.dead) return;
            if (onback_map.TryGetValue(scav, out var onback) && onback != null)
            {
                onback.ChangeOverlap(true);
            }
            if (scavenger != null)
            {
                ChangeOverlap(true);
            }

            foreach (Creature.Grasp grasp in scav.grabbedBy.ToList())
            {
                grasp.Release();
            }

            if (scav.isJunior())
            {
                ScavolutionPlugin.plugin?.ScavengerJunior_GetAdopted((ScavengerAbstractAI)scav.abstractCreature.abstractAI, owner.abstractCreature);
            }

            scavenger = scav;
            onback_map.Add(scav, this);
            ChangeOverlap(false);
            stick = new AbstractJuniorOnBackStick(owner.abstractCreature, scavenger.abstractCreature);
        }

        public void ChangeOverlap(bool newOverlap)
        {
            if (scavenger is null) return;
            scavenger.CollideWithObjects = newOverlap;
            scavenger.canBeHitByWeapons = newOverlap;
            scavenger.GoThroughFloors = !newOverlap;
            if (scavenger.graphicsModule != null && owner.room != null)
            {
                for (int i = 0; i < owner.room.game.cameras.Length; i++)
                {
                    owner.room.game.cameras[i].MoveObjectToContainer(scavenger.graphicsModule, owner.room.game.cameras[i].ReturnFContainer(newOverlap ? "Midground" : "Background"));
                }
            }

            if (newOverlap)
            {
                onback_map.Remove(scavenger);
                stick?.Deactivate();
                stick = null;
                scavenger = null;
            }
        }

        ~JuniorOnBack()
        {
            if (stick is not null)
            {
                stick?.Deactivate();
                stick = null;
            }
        }

    }

    static class JuniorOnBackExtensions
    {
        public static bool CanRetrieveJuniorFromBack(this Player p)
        {
            var onback = GetJuniorOnBack(p);
            if (!ModManager.MSC && !ModManager.CoopAvailable)
            {
                return false;
            }

            if (p.CanRetrieveSpearFromBack || p.CanRetrieveSlugFromBack || onback.scavenger == null || onback.interactionLocked || (p.grasps[0] != null && p.grasps[1] != null))
            {
                return false;
            }
            for (int i = 0; i < p.grasps.Length; i++)
            {
                if (p.grasps[i] != null && (p.Grabability(p.grasps[i].grabbed) > Player.ObjectGrabability.BigOneHand || p.grasps[i].grabbed is Scavenger))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool CanPutJuniorToBack(this Player p, Scavenger junior)
        {
            if (p.slugOnBack != null && p.slugOnBack.slugcat != null) return false;
            var onback = GetJuniorOnBack(p);
            return !onback.interactionLocked && onback.scavenger == null;
        }

        public static JuniorOnBack GetJuniorOnBack(this Creature critter)
        {
            if (JuniorOnBack.creature_map.TryGetValue(critter, out var ret)) return ret;
            return new JuniorOnBack(critter);
        } 
    }


    partial class ScavolutionPlugin
    {
        void JuniorOnBackHooks()
        {
            new Hook(typeof(Player).GetProperty(nameof(Player.CanPutSlugToBack)).GetGetMethod(), PutToBackJuniorFirst);
            new Hook(typeof(Player).GetProperty(nameof(Player.CanPutSpearToBack)).GetGetMethod(), PutToBackJuniorFirst);
            new Hook(typeof(Player).GetProperty(nameof(Player.CanRetrieveSlugFromBack)).GetGetMethod(), PutToBackJuniorFirst);
            new Hook(typeof(Player).GetProperty(nameof(Player.CanRetrieveSpearFromBack)).GetGetMethod(), PutToBackJuniorFirst);

            // scav on players back
            On.Player.Grabability += Player_GrababilityJunior;
            On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdatedJuniorOnBack;
            IL.Player.GrabUpdate += Player_GrabUpdateJuniorOnBack;
            On.Player.GrabUpdate += Player_UpdateJuniorOnBack;
            On.Player.CanIPickThisUp += Player_CanIPickThisUpJunior;
            On.UpdatableAndDeletable.Destroy += UpdatableAndDeletable_DestroyJuniorOnBack;

            // scav on scav back
            On.Scavenger.RecreateSticksFromAbstract += JuniorOnBack_Scavenger_RecreateSticksFromAbstract;
            On.Scavenger.Update += ScavengerJunior_Scavenger_UpdateOnBack;
            IL.Scavenger.GraphicsModuleUpdated += ScavengerJunior_Scavenger_GraphicsModuleUpdated;
            On.Scavenger.Grab += ScavengerJunior_Scavenger_Grab;

            new Hook(typeof(ScavengerAI).GetProperty(nameof(ScavengerAI.HoldWeapon)).GetGetMethod(), ScavengerJunior_ScavengerAI_HoldWeapon);
            On.ScavengerGraphics.ScavengerHand.Update += ScavengerJunior_ScavengerGraphics_ScavengerHand_Update;


            // dont hit juniors on my back
            On.Weapon.HitThisObject += ScavengerJunior_Weapon_HitThisObject;
            IL.Scavenger.MidRangeUpdate += ScavengerJunior_Scavenger_MidRangeUpdate;

        }
        void ScavengerJunior_Scavenger_MidRangeUpdate(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                // 237	02EF	ldfld	class CreatureTemplate/Relationship/Type CreatureTemplate/Relationship::'type'
                // 238	02F4	ldsfld	class CreatureTemplate/Relationship/Type CreatureTemplate/Relationship/Type::Pack
                // 239	02F9	call	bool class ExtEnum`1<class CreatureTemplate/Relationship/Type>::op_Equality(class ExtEnum`1<!0>, class ExtEnum`1<!0>)
                // 240	02FE	brfalse	304 (03F8) ldloc.s V_7 (7)

                ILLabel skip = null!;
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdfld<CreatureTemplate.Relationship>(nameof(CreatureTemplate.Relationship.type)),
                    x => x.MatchLdsfld<CreatureTemplate.Relationship.Type>(nameof(CreatureTemplate.Relationship.Type.Pack)),
                    x => true,
                    x => x.MatchBrfalse(out skip)
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, 7); // index into this.AI.tracker.GetRep() in for loop
                cursor.EmitDelegate((Scavenger self, int creatureTrackedIndex) =>
                {
                    var creature = self.AI.tracker.GetRep(creatureTrackedIndex)?.representedCreature?.realizedCreature;
                    if (creature is not null)
                    {
                        if (self.GetJuniorOnBack().scavenger == creature)
                        {
                            return false;
                        }

                        if (creature is Scavenger)
                        {
                            if (self.grasps.Where(x => x is not null && x.grabbed == creature).FirstOrDefault() is not null)
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                });
                
                cursor.Emit(OpCodes.Brfalse, skip);
            }


            catch (Exception except)
            {
                Logger.LogDebug(except);
            }
            
        }


        bool ScavengerJunior_Weapon_HitThisObject(On.Weapon.orig_HitThisObject orig, Weapon self, global::PhysicalObject obj) {
            try
            {
                if (obj is Scavenger scav)
                {
                    if (JuniorOnBack.onback_map.TryGetValue(scav, out var junioronback) && junioronback.owner == self.thrownBy)
                    {
                        return false;
                    }

                    if (self.thrownBy is Creature thrower)
                    {
                        if (thrower.grasps.Where(x => x is not null && x.grabbed == scav).FirstOrDefault() is not null)
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogDebug(except);   
            }

            return orig(self, obj);
        }

        void ScavengerJunior_Scavenger_UpdateOnBack(On.Scavenger.orig_Update orig, Scavenger self, bool eu)
        {
            try
            {
                var onback = self.GetJuniorOnBack();
                foreach (Creature.Grasp grasp in self.grasps.Where(x => x is not null))
                {
                    if (grasp.grabbed is Scavenger scav)
                    {
                        if (ParentalParams.getOrAdd(scav.AI).wantCarryTimer > 0)
                        {
                            if (onback.scavenger == null)
                            {
                                onback.increment = true;
                            }
                        }
                        else
                        {
                            grasp.Release();   
                        }
                    }
                }

                if (onback.scavenger is not null)
                {
                    if (ParentalParams.getOrAdd(onback.scavenger.AI).wantCarryTimer <= 0)
                    {
                        onback.increment = true;
                    }
                }

                onback.Update();                
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            orig(self, eu);
        }

        bool ScavengerJunior_ScavengerAI_HoldWeapon(Func<ScavengerAI, bool> orig, ScavengerAI self)
        {
            if (self.scavenger.grabbedBy.Where(x => x.grabber is Scavenger).FirstOrDefault() is Creature.Grasp)
            {
                return false;
            }
        
            return orig(self);

        }

        void ScavengerJunior_ScavengerGraphics_ScavengerHand_Update(On.ScavengerGraphics.ScavengerHand.orig_Update orig, ScavengerGraphics.ScavengerHand self)
        {
            orig(self);
            try
            {
                if (self.limbNumber == 0)
                {
                    if (self.scavenger.grabbedBy.Where(x => x.grabber is Scavenger).FirstOrDefault() is Creature.Grasp grabbedBy)
                    {
                        Scavenger grabber = (Scavenger)grabbedBy.grabber;
                        if (grabber.graphicsModule is ScavengerGraphics grabber_graphics)
                        {
                            Vector2 difference = self.scavenger.mainBodyChunk.pos - (Vector2)grabber_graphics.ItemPosition(grabbedBy.graspUsed);
                            self.absoluteHuntPos = self.scavenger.mainBodyChunk.pos - (difference / grab_raidus) * self.armLength * 0.35f;
                            self.mode = Limb.Mode.HuntAbsolutePosition;
                        }
                    }
                }

                if (self.limbNumber == 1)
                {
                    if (self.scavenger.grasps.Where(x => x != null && x.grabbed is Scavenger).FirstOrDefault() is Creature.Grasp grabbing)
                    {
                        Scavenger grabbed = (Scavenger)grabbing.grabber;
                        Vector2 difference = grabbed.mainBodyChunk.pos - (Vector2)self.graphics.ItemPosition(grabbing.graspUsed);
                        self.absoluteHuntPos = grabbed.mainBodyChunk.pos - (difference / grab_raidus) * self.armLength * 0.35f;
                        self.mode = Limb.Mode.HuntAbsolutePosition;
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            
        }
        const float grab_raidus = 35f;
        void ScavengerJunior_Scavenger_GraphicsModuleUpdated(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.Emit(OpCodes.Ldarg_2);
                cursor.EmitDelegate((Scavenger self, bool actuallyViewed, bool eu) =>
                {
                    var onback = self.GetJuniorOnBack();
                    onback.GraphicsModuleUpdated(actuallyViewed, eu);
                });



                int item_loc = 1;
                int item_pos_loc = 2;

                //38	0077	stloc.2
                cursor.GotoNext(MoveType.After,
                    x => x.MatchStloc(item_pos_loc)
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, item_loc);
                cursor.Emit(OpCodes.Ldloc, item_pos_loc);
                cursor.Emit(OpCodes.Ldarg_2);
                cursor.EmitDelegate((Scavenger self, PhysicalObject grabbed_obj, Vector2 itemPos, bool eu) =>
                {
                    if (grabbed_obj is Scavenger scav)
                    {
                        Vector2 difference = scav.mainBodyChunk.pos - itemPos;

                        if (difference.sqrMagnitude > grab_raidus * grab_raidus)
                        {
                            Vector2 difference_normal = difference.normalized;
                            Vector2 targetpos = itemPos + difference_normal * grab_raidus;
                            scav.mainBodyChunk.MoveFromOutsideMyUpdate(eu, targetpos);

                            var leaving_magnitude = Vector2.Dot(scav.mainBodyChunk.vel, difference_normal);
                            var weightdiff = scav.TotalMass / self.TotalMass;
                            if (leaving_magnitude > 0f)
                            {
                                var leaving_vel = difference_normal * leaving_magnitude;
                                scav.mainBodyChunk.vel -= leaving_vel * 0.5f * Math.Max(weightdiff, 1.0f);
                                self.mainBodyChunk.vel += leaving_vel * (1.0f / weightdiff) * 0.01f;
                            }

                            scav.abstractCreature.abstractAI.SetDestination(self.abstractCreature.abstractAI.destination);
                            scav.AI.pathFinder.SetDestination(self.abstractCreature.abstractAI.destination);
                            scav.AI.runSpeedGoal = self.AI.runSpeedGoal;

                            if (scav.grasps[0] != null && self.Consious)
                            {
                                scav.ArrangeInventory();
                            }
                        }

                        return true;
                    }

                    return false;
                });

                cursor.FindNext(out var ret_cursor, x => x.Match(OpCodes.Ret));
                cursor.Emit(OpCodes.Brtrue, ret_cursor[0].MarkLabel());
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        bool ScavengerJunior_Scavenger_Grab(On.Scavenger.orig_Grab orig, Scavenger self, PhysicalObject obj, int graspUsed, int chunkGrabbed, Creature.Grasp.Shareability shareability, float dominance, bool overrideEquallyDominant, bool pacifying)
        {
            try
            {
                if (obj is Scavenger scavcarry)
                {
                    pacifying = false;
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            return orig(self, obj, graspUsed, chunkGrabbed, shareability, dominance, overrideEquallyDominant, pacifying);
        }

        bool ScangerJunior_WantToBeHeld(ScavengerAI scav, ScavengerAI grabber)
        {
            if (scav.scavenger.animation?.id == DLCSharedEnums.ScavengerAnimationID.Jumping)
            {
                ParentalParams.getOrAdd(scav).wantCarryTimer = 0;
                return false;
            } 
            if ((scav.creature.abstractAI as ScavengerAbstractAI)!.GoHome()) return false;
            if (scav.creature.controlled) return false;
            if (!ScavengerParentTracker.map.TryGetValue(scav, out var parentTracker)) return false;
            if (parentTracker.tiredness > 100) return true;
            if (scav.threatTracker.Utility() > 0.6) return true;
            if (grabber.threatTracker.Utility() > 0.6) return true;
            if (grabber.agitation > 0.7) return true;
            return false;
        }

        void JuniorOnBack_Scavenger_RecreateSticksFromAbstract(On.Scavenger.orig_RecreateSticksFromAbstract orig, Scavenger self)
        {
            orig(self);
            try
            {
                foreach (Creature.Grasp grasp in self.grasps.Where(x => x != null))
                {
                    if (grasp.pacifying && grasp.grabbed is Scavenger)
                    {
                        grasp.pacifying = false;
                    }
                }

                foreach (AbstractPhysicalObject.AbstractObjectStick stick in self.abstractCreature.stuckObjects.ToList())
                {
                    if (stick.A != self.abstractCreature) continue;
                    if (stick is JuniorOnBack.AbstractJuniorOnBackStick)
                    {
                        stick.Deactivate();
                        self.GetJuniorOnBack().ScavtoBack((Scavenger)stick.B.realizedObject);
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

        }

        void UpdatableAndDeletable_DestroyJuniorOnBack(On.UpdatableAndDeletable.orig_Destroy orig, UpdatableAndDeletable self)
        {
            orig(self);
            try
            {
                if (self is Creature critter && (critter is Player || critter is Scavenger))
                {
                    critter.GetJuniorOnBack().ChangeOverlap(true);
                } 
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        bool Player_CanIPickThisUpJunior(On.Player.orig_CanIPickThisUp orig, global::Player self, global::PhysicalObject obj)
        {
            if (obj is Scavenger scav)
            {
                if (JuniorOnBack.onback_map.TryGetValue(scav, out _) || self.isNPC || self.playerState.isPup)
                {
                    return false;
                }
            }

            return orig(self, obj);
        }
        void Player_UpdateJuniorOnBack(On.Player.orig_GrabUpdate orig, Player self, bool eu)
        {
            try
            {
                self.GetJuniorOnBack().Update();
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self, eu);
        }

        void Player_GrabUpdateJuniorOnBack(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                // 1336	0FFA	ldloc.s	V_7 (7)
                // 1337	0FFC	ldc.i4.m1
                // 1338	0FFD	bgt.s	1342 (1007) ldarg.0 
                // 1340	1000	call	instance bool Player::get_CanRetrieveSlugFromBack()
                // 1341	1005	brfalse.s	1346 (1013) ldarg.0 
                // 1342	1007	ldarg.0
                // 1343	1008	ldfld	class Player/SlugOnBack Player::slugOnBack
                // 1344	100D	ldc.i4.1
                // 1345	100E	stfld	bool Player/SlugOnBack::increment

                int slugindex_loc = -1;
                cursor.GotoNext(MoveType.AfterLabel,
                    x => x.MatchLdloc(out slugindex_loc),
                    x => x.MatchLdcI4(-1),
                    x => x.MatchBgt(out _),


                    x => x.MatchLdarg(0),
                    x => x.MatchCall(typeof(Player).GetProperty(nameof(Player.CanRetrieveSlugFromBack)).GetGetMethod()),
                    x => x.MatchBrfalse(out _),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<Player>(nameof(Player.slugOnBack)),
                    x => x.MatchLdcI4(1),
                    x => x.MatchStfld<Player.SlugOnBack>(nameof(Player.SlugOnBack.increment))
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc, slugindex_loc);
                cursor.Emit(OpCodes.Ldloc, 6); // use_item;
                cursor.EmitDelegate((Player self, int slugindex, int use_item) =>
                {
                    if (use_item != -1) return;
                    if (slugindex <= -1)
                    {
                        Scavenger? scavenger_grabbed = null;
                        for (int i = 0; i < 2; i++)
                        {
                            if (self.grasps[i] != null && self.grasps[i].grabbed is Scavenger scav && !scav.dead)
                            {
                                scavenger_grabbed = scav;
                            }
                        }

                        if (self.input[0].pckp && ((scavenger_grabbed != null && self.CanPutJuniorToBack(scavenger_grabbed)) || self.CanRetrieveJuniorFromBack()))
                        {
                            self.GetJuniorOnBack().increment = true;
                        }
                    }
                });

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        void Player_GraphicsModuleUpdatedJuniorOnBack(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
        {
            try
            {
                self.GetJuniorOnBack().GraphicsModuleUpdated(actuallyViewed, eu);
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self, actuallyViewed, eu);
        }

        delegate bool orig_canPutToBack(Player self);
        bool PutToBackJuniorFirst(orig_canPutToBack orig, Player self)
        {
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    if (self.grasps[i] != null)
                    {
                        if (self.grasps[i].grabbed is Scavenger)
                        {
                            return false;
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


        Player.ObjectGrabability Player_GrababilityJunior(On.Player.orig_Grabability orig, Player self, PhysicalObject obj)
        {
            try
            {
                if (obj is Scavenger scav && scav.isJunior()) return Player.ObjectGrabability.BigOneHand;

                if (obj.grabbedBy.FirstOrDefault() is Creature.Grasp grasp)
                {
                    if (grasp.grabber is Scavenger scav2 && scav2.isJunior())
                    {
                        if (scav2.grabbedBy.Count() <= 0)
                        {
                            return Player.ObjectGrabability.CantGrab;
                        }
                    } 
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            return orig(self, obj);
        }
    }
}