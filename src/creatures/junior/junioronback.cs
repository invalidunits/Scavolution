using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using SprobDesecratingGraves;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace Scavolution
{
    public class JuniorOnBack
    {
        static public ConditionalWeakTable<Creature, JuniorOnBack> creature_map = new();
        static public ConditionalWeakTable<Scavenger, JuniorOnBack> onback_map = new();
        public readonly Creature owner;
        public Scavenger? scavenger { get; private set; }
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
                var backpacktime = this.owner is Player ? 20 : 60;

                this.counter++;
                if (this.counter > backpacktime)
                {
                    if (this.scavenger != null)
                    {
                        owner.room.PlaySound(SoundID.Slugcat_Switch_Hands_Init, owner.mainBodyChunk);
                        this.ScavtoHand();
                        this.counter = 0;
                    }
                    else if (this.scavenger == null)
                    {
                        for (int i = 0; i < this.owner.grasps.Length; i++)
                        {
                            if (this.owner.grasps[i] != null && this.owner.grasps[i].grabbed is Scavenger scav && scav.isJunior())
                            {
                                owner.room.PlaySound(SoundID.Slugcat_Switch_Hands_Init, owner.mainBodyChunk);
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
                                if (owner is Player p2)
                                {
                                    direction = p2.flipDirection > 0f;
                                }

                                jumpFinder.bestJump = new Scavenger.JumpFinder.JumpInstruction(scavenger.mainBodyChunk.pos, new Vector2(direction ? (-11.5f) : 11.5f, 13.5f), 0.5f + UnityEngine.Random.Range(-0.1f, 0.1f));
                                PathFinder.PathingCell goalCell = scavenger.AI.pathFinder.PathingCellAtWorldCoordinate(scavenger.abstractCreature.pos + new IntVector2(direction ? -10 : 10, 0));
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

            if (ScavolutionPlugin.NotSlugcatPlayables)
            {
                NotSlugcatPlayablesUpdate();
            }

            if (this.interactionLocked && !this.increment) this.interactionLocked = false;
            this.increment = false;
        }

        public void NotSlugcatPlayablesUpdate()
        {
            if (scavenger is null) return;

            var scavdata = scavenger.GetScavengerData();
            if (scavdata.controller != null && scavdata.noJump <= 0 && scavdata.controller.input[0].jmp && scavdata.jumpTimer <= 0 && !scavdata.controller.input[1].jmp)
            {
                for (int k = 0; k < scavenger.bodyChunks.Length; k++)
                {
                    Vector2 vector = new Vector2((float)scavenger.GetScavengerData().controller.input[0].x * 0.35f, ((float)scavenger.GetScavengerData().controller.input[0].y >= 0f) ? 1f : (-1f));
                    scavenger.bodyChunks[k].vel += vector * 11f * k switch
                    {
                        1 => 1.25f,
                        2 => 0.65f,
                        _ => 1f,
                    };
                }

                scavdata.jumpTimer = 10;
                scavenger.room.PlaySound(SoundID.Slugcat_Normal_Jump, scavenger.mainBodyChunk);
                this.ChangeOverlap(true);
            }
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

            scavenger.knucklePos = null;
            scavenger.movMode = Scavenger.MovementMode.Run;
            scavenger.moveModeChangeCounter = Mathf.Max(this.scavenger.moveModeChangeCounter, 5);
            scavenger.shortcutDelay = Mathf.Max(this.scavenger.shortcutDelay, 5);
            scavenger.enteringShortCut = null;
            scavenger.flip = Mathf.Lerp(scavenger.flip, 0f, 0.01f);

            if (owner is Player p)
            {
                var restpos = (owner.graphicsModule is PlayerGraphics playerGraphics) ? playerGraphics.head.pos : owner.mainBodyChunk.pos;
                restpos += new Vector2(0, 14f);

                var offset = restpos - scavenger.bodyChunks[0].pos;
                scavenger.bodyChunks[0].HardSetPosition(restpos);
                scavenger.bodyChunks[2].RelativeMoveFromOutsideMyUpdate(eu, offset);

                if (ModManager.DLCShared)
                {
                    if (scavenger.animation?.id != DLCSharedEnums.ScavengerAnimationID.PrepareToJump)
                    {
                        scavenger.bodyChunks[0].vel = owner.mainBodyChunk.vel; // torsoe
                        scavenger.bodyChunks[1].vel = Vector2.Lerp(scavenger.bodyChunks[1].vel, owner.mainBodyChunk.vel, 0.5f); // legs
                    }
                }


                // no vel sync for head
            }

            if (owner is Scavenger scav_holder)
            {
                Vector2 headpos = scav_holder.mainBodyChunk.pos;
                headpos += new Vector2(-scav_holder.flip * 5f, 23f);

                var offset = headpos - scavenger.bodyChunks[0].pos;
                scavenger.bodyChunks[0].HardSetPosition(headpos);
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

        public bool NotSlugcatPlayableisRattatouillie(Scavenger scav)
        {
            Player rattouillie_controller = scav.GetScavengerData().controller;
            if (rattouillie_controller?.SlugCatClass == ScavolutionPlugin.PlayerJunior)
            {
                if (owner.GetCreatureData().controller == rattouillie_controller)
                {
                    return true;
                }
            }

            return false;
        }

        public void ScavtoHand()
        {
            if (scavenger == null) return;
            if (!(ScavolutionPlugin.NotSlugcatPlayables && NotSlugcatPlayableisRattatouillie(scavenger)))
            {
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
            }
            

            ChangeOverlap(true);
            interactionLocked = true;
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

            scavenger = scav;
            onback_map.Add(scav, this);
            ChangeOverlap(false);
            stick = new AbstractJuniorOnBackStick(owner.abstractCreature, scavenger.abstractCreature);

            if (scav.isJunior())
            {
                ScavolutionPlugin.plugin?.ScavengerJunior_GetAdopted((ScavengerAbstractAI)scav.abstractCreature.abstractAI, owner.abstractCreature);
            }
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

            if (ScavolutionPlugin.NotSlugcatPlayables)
            {
                NotSlugcatPlayableRattatouillie(newOverlap);
            }

            if (newOverlap)
            {
                onback_map.Remove(scavenger);
                stick?.Deactivate();
                stick = null;
                scavenger = null;
            }

        }

        public void NotSlugcatPlayableRattatouillie(bool newOverlap)
        {
            var onbackdata = scavenger.GetCreatureData();
            var ownerdata = owner.GetCreatureData();

            if (newOverlap)
            {
                if (onbackdata.controller is not null &&
                    ownerdata.controller == onbackdata.controller && 
                    onbackdata.controller.SlugCatClass == ScavolutionPlugin.PlayerJunior
                    )
                {
                    ownerdata.SetController(null);
                }
            }
            else
            {
                if (onbackdata.controller is not null && ownerdata.controller is null && 
                    onbackdata.controller.SlugCatClass == ScavolutionPlugin.PlayerJunior)
                {
                    ownerdata.SetController(onbackdata.controller);
                }
            }
        }

        public void Throw(bool eu)
        {
            if (scavenger is null) return;
            Scavenger? thrownScav = scavenger;
            ScavtoHand();
            Creature.Grasp scavGrasp = thrownScav.grabbedBy.FirstOrDefault(x => x.grabber == owner);
            if (owner is Player p)
            {
                p.ThrowObject(scavGrasp.graspUsed, eu);
            }
            else if (owner is Scavenger scav)
            {
                scav.SwitchGrasps(0, scavGrasp.graspUsed);
                this.scavenger.Throw(new Vector2(this.scavenger.flip * 250f, 0f));
                this.scavenger.ReleaseGrasp(0);
            }
            else
            {
                owner.ReleaseGrasp(scavGrasp.graspUsed);
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
            if (p.CanRetrieveSlugFromBack || p.CanRetrieveSpearFromBack) return false;
            var onback = GetJuniorOnBack(p);
            return !onback.interactionLocked && onback.scavenger == null && junior.Consious;
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
            new Hook(typeof(AbstractCreature).GetProperty(nameof(AbstractCreature.PacifiedBecauseCarried)).GetGetMethod(), ScavengerJunior_PacifiedBecauseCarried);

            // scav on players back
            On.Player.Grabability += Player_GrababilityJunior;
            On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdatedJuniorOnBack;
            IL.Player.GrabUpdate += Player_GrabUpdateJuniorOnBack;
            On.Player.CanIPickThisUp += Player_CanIPickThisUpJunior;
            On.UpdatableAndDeletable.Destroy += UpdatableAndDeletable_DestroyJuniorOnBack;

            // scav on scav back
            On.Scavenger.RecreateSticksFromAbstract += JuniorOnBack_Scavenger_RecreateSticksFromAbstract;
            On.Scavenger.Update += ScavengerJunior_Scavenger_UpdateOnBack;
            IL.Scavenger.GraphicsModuleUpdated += ScavengerJunior_Scavenger_GraphicsModuleUpdated;
            On.Creature.Grab += ScavengerJunior_Creature_Grab;

            // scav on back
            On.Scavenger.Act += JuniorOnBack_ScavengerAct;

            // graphical stuff
            new Hook(typeof(ScavengerGraphics.ScavengerHand).GetMethod(nameof(ScavengerGraphics.ScavengerHand.CheckForGrabPos),
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                ), ScavengerJunior_ScavengerGraphics_ScavengerHand_CheckForGrabPos);
            new Hook(typeof(ScavengerAI).GetProperty(nameof(ScavengerAI.HoldWeapon)).GetGetMethod(), ScavengerJunior_ScavengerAI_HoldWeapon);
            On.ScavengerGraphics.ScavengerHand.Update += ScavengerJunior_ScavengerGraphics_ScavengerHand_Update;
            On.Scavenger.KnucklePosLegal += ScavengerJunior_KnucklePosLegal;

            new Hook(typeof(Scavenger).GetProperty(nameof(Scavenger.MovementSpeed)).GetGetMethod(), ScavengerJunior_MovementSpeed);
            new Hook(typeof(Scavenger).GetProperty(nameof(Scavenger.LittleStuck)).GetGetMethod(), ScavengerJunior_MovementSpeed);
            new Hook(typeof(Scavenger).GetProperty(nameof(Scavenger.ReallyStuck)).GetGetMethod(), ScavengerJunior_MovementSpeed);

            // safari
            IL.Scavenger.LookForItemsToPickUp += ScavengerJunior_Scavenger_LookForItemsToPickUp;


            // dont hit juniors on my back or being grabbed by enemy
            On.Weapon.HitThisObject += ScavengerJunior_Weapon_HitThisObject;
            IL.Scavenger.MidRangeUpdate += ScavengerJunior_Scavenger_MidRangeUpdate;

            if (NotSlugcatPlayables)
            {
                NotSlugcatPlayables_JuniorOnBackHooks();
            }
        }

        bool ScavengerJunior_PacifiedBecauseCarried(Func<AbstractCreature, bool> orig, AbstractCreature creature)
        {
            if (creature.stuckObjects.Any(x => x is JuniorOnBack.AbstractJuniorOnBackStick stick && stick.B == creature)) return true;
            return orig(creature);
        }

        float ScavengerJunior_MovementSpeed(Func<Scavenger, float> orig, global::Scavenger self)
        {
            if (JuniorOnBack.onback_map.TryGetValue(self, out _)) return 0.0f;
            return orig(self);
        }

        bool ScavengerJunior_AllowIdleMoves(Func<Scavenger, bool> orig, global::Scavenger self)
        {
            if (JuniorOnBack.onback_map.TryGetValue(self, out _)) return false;
            return orig(self);
        }

        bool ScavengerJunior_KnucklePosLegal(On.Scavenger.orig_KnucklePosLegal orig, global::Scavenger self, Vector2? testPos)
        {
            if (JuniorOnBack.onback_map.TryGetValue(self, out _)) return false;
            return orig(self, testPos);
        }

        static public List<ILHook> junior_on_back_nsp_hooks = [];
        void NotSlugcatPlayables_JuniorOnBackHooks()
        {
            junior_on_back_nsp_hooks.Add(new ILHook(typeof(SprobDesecratingGraves.ScavengerHooks).GetMethod(nameof(SprobDesecratingGraves.ScavengerHooks.GraphicsModuleUpdated),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                ), NotSlugcatPlayables_Scavenger_GraphicsModuleUpdate));
            junior_on_back_nsp_hooks.Add(new ILHook(typeof(SprobDesecratingGraves.ScavengerHooks).GetMethod(nameof(SprobDesecratingGraves.ScavengerHooks.ControlledAct),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                ), NotSlugcatPlayables_Scavenger_ControlledAct));
            junior_on_back_nsp_hooks.Add(new ILHook(typeof(SprobDesecratingGraves.ScavengerHooks).GetMethod(nameof(SprobDesecratingGraves.ScavengerHooks.ScavengerControlledBehavior),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public
                ), NotSlugcatPlayables_Scavenger_ScavengerControlledBehavior));
            foreach (IDetour hook in junior_on_back_nsp_hooks)
            {
                hook.Apply();
            }
        }

        void NotSlugcatPlayables_Scavenger_ScavengerControlledBehavior(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);

                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate((ScavengerAI self) =>
                {
                    Scavenger scavenger = self.scavenger;
                    if (scavenger.isJunior() && isNotSlugcatsPlayer(self.creature, out _))
                    {
                        var data = scavenger.abstractCreature.GetScavengerData();
                        if (JuniorOnBack.onback_map.TryGetValue(scavenger, out _))
                        {
                            if (scavenger.inputWithDiagonals?.y != 1)
                            {
                                data.noJump = Mathf.Max(data.noJump, 1);
                            }
                            data.wall = true;
                        }   
                    }
                });
                


                cursor.GotoNext(MoveType.Before, 
                    x => x.MatchLdarg(1),
                    x => x.MatchLdfld<ScavengerAI>(nameof(ScavengerAI.scavenger)),
                    x => x.MatchCall(typeof(ScavengerHooks).GetMethod(nameof(ScavengerHooks.LookForItemsToPickUp), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)));
                
                ILLabel label = cursor.DefineLabel();
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate((ScavengerAI self) =>
                {
                    Scavenger scavenger = self.scavenger;
                    if (ScavengerPendulum_OffGround(scavenger))
                    {
                        foreach (PhysicalObject obj in scavenger.room.physicalObjects[scavenger.collisionLayer])
                        {
                            float range = 26 + scavenger.bodyChunks[1].rad;
                            if (obj is Creature creature && ((obj is Player p && p.CanPutJuniorToBack(scavenger) && !p.isNPC) || (obj is Scavenger scav && !scav.isJunior())))
                            {
                                JuniorOnBack onBack = creature.GetJuniorOnBack();
                                if (onBack.scavenger != null) continue;
                                if (creature == scavenger) continue;
                                if (creature.abstractCreature.GetAllConnectedObjects().Contains(scavenger.abstractCreature)) continue;
                                if (!Custom.DistLess(scavenger.bodyChunks[1].pos, creature.mainBodyChunk.pos, range)) continue;
                                if (!creature.Consious) continue;
                                onBack.ScavtoBack(scavenger);
                                return true;
                            }
                        }
                    }
                    return false;
                });
                cursor.Emit(OpCodes.Brtrue, label);
                cursor.Index += 3;
                cursor.MarkLabel(label);
            }
            catch (Exception except)
            {
                Logger.LogDebug(except);
            }
        }

        void NotSlugcatPlayables_Scavenger_ControlledAct(ILContext context)
        {

            try
            {

                // 1493	10CF	stloc.s	V_59 (59)
                // 1494	10D1	ldloc.s	V_59 (59)
                // 1495	10D3	brfalse	1968 (158D) nop 
                ILCursor cursor = new(context);
                ILLabel breakifblock = null!;
                cursor.GotoNext(MoveType.After,
                    x => x.MatchStloc(59),
                    x => x.MatchLdloc(59),
                    x => x.MatchBrfalse(out breakifblock)
                );

                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate((Scavenger self) =>
                {
                    bool ret = false;
                    var onback = self.GetJuniorOnBack();
                    if ((self.grasps.Any(x => x?.grabbed is Scavenger) && onback.scavenger == null) || (onback.scavenger != null && self.grasps.Any(x => x is null)))
                    {
                        self.GetJuniorOnBack().increment = true;
                        ret = true;
                    }

                    return ret;
                });



                cursor.Emit(OpCodes.Brtrue, breakifblock);
                
            }
            catch (Exception except)
            {
                Logger.LogDebug(except);
            }
        }

        void NotSlugcatPlayables_Scavenger_GraphicsModuleUpdate(ILContext context)
        {
            try
            {

                // 7	0010	ldloc.0
                // 8	0011	brfalse	364 (03F8) ldarg.0 

                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdloc(0),
                    x => x.MatchBrfalse(out _)
                );

                cursor.Emit(OpCodes.Ldarg_1);
                cursor.Emit(OpCodes.Ldarg_2);
                cursor.Emit(OpCodes.Ldarg_3);
                cursor.EmitDelegate((Scavenger self, bool actuallyViewed, bool eu) =>
                {
                    var onback = self.GetJuniorOnBack();
                    onback.GraphicsModuleUpdated(actuallyViewed, eu);
                });

                // 234	0298	br	348 (03DC) nop 
                // 235	029D	nop
                // 236	029E	ldarg.1
                // 237	029F	callvirt	instance class ['Assembly-CSharp']GraphicsModule ['Assembly-CSharp']PhysicalObject::get_graphicsModule()
                // 238	02A4	ldnull
                // 239	02A5	ceq
                ILLabel leaveelseblock = null!;
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(1),
                    x => x.MatchCallvirt(typeof(PhysicalObject).GetProperty(nameof(PhysicalObject.graphicsModule)).GetGetMethod()),
                    x => x.MatchLdnull(),
                    x => x.MatchCeq()
                );
                cursor.GotoPrev(x => x.MatchBr(out leaveelseblock));
                cursor.GotoNext(MoveType.After,
                    x => x.MatchCall(typeof(SprobDesecratingGraves.ScavengerHooks).GetMethod(
                        nameof(SprobDesecratingGraves.ScavengerHooks.ItemPosition),
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)),
                    x => x.MatchCall(out _), // conversion float2 to Vector2
                    x => x.MatchStloc(14));


                Logger.LogDebug(context.Method.Parameters[1].ParameterType);
                Logger.LogDebug(context.Body.Variables[3].VariableType);
                Logger.LogDebug(context.Body.Variables[14].VariableType);
                Logger.LogDebug(context.Method.Parameters[3].ParameterType);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.Emit(OpCodes.Ldloc, 3);
                cursor.Emit(OpCodes.Ldloc, 14);
                cursor.Emit(OpCodes.Ldarg_3);
                cursor.EmitDelegate((Scavenger self, PhysicalObject grabbed_obj, Vector2 itemPos, bool eu) =>
                {
                    if (grabbed_obj is Scavenger scav)
                    {
                        HandholdWithJunior(self, scav, itemPos, eu);
                        return true;
                    }

                    return false;
                });

                cursor.Emit(OpCodes.Brtrue, leaveelseblock);
            }
            catch (Exception except)
            {
                Logger.LogDebug(except);
            }
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
                        if (creature is Scavenger otherscav)
                        {
                            if (JuniorOnBack.onback_map.TryGetValue(otherscav, out var onback1) && onback1.owner == self)
                            {
                                return false;
                            }

                            if (JuniorOnBack.onback_map.TryGetValue(self, out var onback2) && onback1.owner == otherscav)
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

        void ScavengerJunior_Scavenger_LookForItemsToPickUp(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.After,
                    x => x.MatchLdloc(5),
                    x => x.MatchIsinst<AbstractCreature>()
                );

                cursor.Emit(OpCodes.Ldarg, 0);
                cursor.Emit(OpCodes.Ldloc, 5);
                cursor.EmitDelegate((object instcheck, Scavenger self, AbstractPhysicalObject obj) =>
                {
                    if (obj is AbstractCreature critter && critter.creatureTemplate.type == SECreatureEnums.ScavengerJunior && !self.isJunior())
                    {
                        return obj;
                    }

                    return instcheck;
                });
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

        }



        // void ScavengerJunior_ScavengerAI_ControlledBehavior(On.ScavengerAI.orig_ControlledBehavior orig, ScavengerAI self)
        // {
        //     try
        //     {
        //         var onback = self.scavenger.GetJuniorOnBack();
        //         if (self.scavenger.inputWithDiagonals.HasValue)
        //         {
        //             if (self.scavenger.inputWithDiagonals.Value.pckp && ((self.scavenger.grasps.Any(x => x is null) && onback.scavenger != null) || (onback.scavenger == null && self.scavenger.grasps[0]?.grabbed is Scavenger)))
        //             {
        //                 onback.increment = true;
        //             }
        //             else
        //             {
        //                 onback.increment = false;
        //             }
        //         }

        //         onback.Update();
        //     }
        //     catch (Exception except)
        //     {
        //         Logger.LogError(except);
        //     }

        //     orig(self);
        // }


        bool ScavengerJunior_Weapon_HitThisObject(On.Weapon.orig_HitThisObject orig, Weapon self, global::PhysicalObject obj)
        {
            try
            {
                if (obj is Scavenger scav)
                {
                    if (self.thrownBy is Scavenger scavthrower)
                    {
                        if (JuniorOnBack.onback_map.TryGetValue(scav, out var onback1) && onback1.owner == scavthrower)
                        {
                            return false;
                        }

                        if (JuniorOnBack.onback_map.TryGetValue(scavthrower, out var onback2) && onback1.owner == scav)
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
            orig(self, eu);
            try
            {
                var onback = self.GetJuniorOnBack();
                if (!ControlledScavenger(self.abstractCreature))
                {
                    
                    if (self.Consious)
                    {
                        bool injured = self.Injured > 0f;
                        foreach (Creature.Grasp grasp in self.grasps.Where(x => x is not null))
                        {
                            
                            if (grasp.grabbed is Scavenger scav)
                            {
                                bool validtoDrop = ScavengerJunior_IsValidDrop(scav.AI, self.AI);
                                if (validtoDrop && (ParentalParams.getOrAdd(scav.AI).wantCarryTimer <= 0 || scav.AI.giftForMe != null))
                                {
                                    grasp.Release();
                                }
                                else if (validtoDrop || ParentalParams.getOrAdd(scav.AI).wantCarryTimer > 100)
                                {
                                    if (onback.scavenger == null && !injured)
                                    {
                                        onback.increment = true;
                                    }
                                }
                            }
                        }

                        if (self.enteringShortCut.HasValue && onback.scavenger != null && ControlledScavenger(onback.scavenger.abstractCreature))
                        {
                            if (!self.room.shortcutData(self.enteringShortCut.Value).LeadingSomewhere)
                            {
                                onback.ChangeOverlap(true);
                            }
                        }


                        if (onback.scavenger is not null)
                        {
                            

                            bool validtoDrop = ScavengerJunior_IsValidDrop(onback.scavenger.AI, self.AI);
                            if (validtoDrop)
                            {
                                if (ParentalParams.getOrAdd(onback.scavenger.AI).wantCarryTimer <= 100 || injured)
                                {
                                    onback.increment = true;
                                }

                                if (onback.scavenger.AI.giftForMe != null)
                                {
                                    onback.Throw(eu);
                                }
                            }
                        }
                    }
                }

                onback.Update();
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        bool ScavengerJunior_ScavengerAI_HoldWeapon(Func<ScavengerAI, bool> orig, ScavengerAI self)
        {
            if (self.scavenger.grabbedBy.Where(x => x.grabber is Scavenger).FirstOrDefault() is Creature.Grasp)
            {
                return false;
            }

            return orig(self);

        }

        static public float2 ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(ScavengerGraphics.ScavengerHand scavengerHand, float timeStacker = 0.0f)
        {
            float2 joint = math.lerp(scavengerHand.graphics.drawPositions[scavengerHand.graphics.chestDrawPos, 1], scavengerHand.graphics.drawPositions[scavengerHand.graphics.chestDrawPos, 0], timeStacker);
            joint += Custom.PerpendicularVector(
                (joint - math.normalize(math.lerp(scavengerHand.graphics.drawPositions[scavengerHand.graphics.hipsDrawPos, 1], scavengerHand.graphics.drawPositions[scavengerHand.graphics.hipsDrawPos, 0], timeStacker)))) *
                (1f - Mathf.Abs(Mathf.Lerp(scavengerHand.graphics.lastFlip, scavengerHand.graphics.flip, timeStacker))) * 
                10f * (((float)scavengerHand.limbNumber == 0f) ? (-1f) : 1f);
            joint += Custom.DirVec(math.lerp(scavengerHand.graphics.drawPositions[scavengerHand.graphics.hipsDrawPos, 1], scavengerHand.graphics.drawPositions[scavengerHand.graphics.hipsDrawPos, 0], timeStacker), math.lerp(scavengerHand.graphics.drawPositions[scavengerHand.graphics.chestDrawPos, 1], scavengerHand.graphics.drawPositions[scavengerHand.graphics.chestDrawPos, 0], timeStacker)) * 5f;
            return new Vector2(joint.x, joint.y);
        }

        float2? ScavengerJunior_ScavengerGraphics_ScavengerHand_CheckForGrabPos(Func<ScavengerGraphics.ScavengerHand, float2?> orig, ScavengerGraphics.ScavengerHand self)
        {
            try
            {
                if (JuniorOnBack.onback_map.TryGetValue(self.scavenger, out var onback))
                {
                    if (onback.owner is Scavenger scav)
                    {
                        if (scav.graphicsModule is ScavengerGraphics graphics)
                        {
                            return ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(graphics.hands[self.limbNumber]);
                        }
                        else
                        {
                            return null!;
                        }
                    }

                    if (onback.owner is Player p)
                    {
                        if (p.graphicsModule is PlayerGraphics graphics)
                        {
                            return graphics.hands[self.limbNumber].pos;
                        }
                        else
                        {
                            return null!;
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

        void ScavengerJunior_ScavengerGraphics_ScavengerHand_Update(On.ScavengerGraphics.ScavengerHand.orig_Update orig, ScavengerGraphics.ScavengerHand self)
        {

            try
            {
                orig(self);
                if (self.limbNumber == 0)
                {
                    if (self.scavenger.grabbedBy.Where(x => x.grabber is Scavenger).FirstOrDefault() is Creature.Grasp grabbedBy)
                    {
                        Scavenger grabber = (Scavenger)grabbedBy.grabber;
                        if (grabber.graphicsModule is ScavengerGraphics grabber_graphics)
                        {
                            self.pos = grabber_graphics.hands[1].pos;
                            self.vel = Vector2.zero;
                            self.reachedSnapPosition = true;
                            return;
                        }
                    }
                }

                if (self.limbNumber == 1)
                {
                    if (self.scavenger.grasps.Where(x => x != null && x.grabbed is Scavenger).FirstOrDefault() is Creature.Grasp grabbing)
                    {
                        Scavenger grabbed = (Scavenger)grabbing.grabbed;
                        float2 ourshoulderpos = ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(self);
                        float2 grabbedshoulderpos = grabbed.graphicsModule is ScavengerGraphics grabbedgraphics ?
                            ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(grabbedgraphics.hands[0]) : grabbed.mainBodyChunk.pos;
                        self.pos = Vector2.Lerp(ourshoulderpos, grabbedshoulderpos, 0.5f);
                        self.vel = Vector2.zero;
                        self.reachedSnapPosition = true;
                        return;
                    }
                }

                var gesturing = (self.limbNumber == 0 && self.scavenger.animation is Scavenger.ThrowAnimation) ||
                                (self.limbNumber == 0 && self.scavenger.animation is Scavenger.ThrowChargeAnimation) ||
                                (self.scavenger.animation is Scavenger.PointingAnimation panim && self.limbNumber == panim.PointingArm) ||
                                (self.scavenger.animation is Scavenger.CommunicationAnimation canim && self.limbNumber == canim.GestureArm);

                if (!gesturing && JuniorOnBack.onback_map.TryGetValue(self.scavenger, out var onback))
                {
                    if (onback.owner is Scavenger scav)
                    {
                        if (scav.graphicsModule is ScavengerGraphics graphics)
                        {
                            self.pos = ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(graphics.hands[self.limbNumber]);
                            self.reachedSnapPosition = true;
                        }
                    }

                    if (onback.owner is Player p)
                    {
                        if (p.graphicsModule is PlayerGraphics graphics)
                        {
                            self.pos = graphics.hands[self.limbNumber].pos;
                            self.reachedSnapPosition = true;
                        }
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        const float grab_raidus = 60f;
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
                        HandholdWithJunior(self, scav, itemPos, eu);
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

        void HandholdWithJunior(Scavenger self, Scavenger scav, Vector2 itemPos, bool eu)
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
            }
        }

        bool ScavengerJunior_Creature_Grab(On.Creature.orig_Grab orig, Creature self, PhysicalObject obj, int graspUsed, int chunkGrabbed, Creature.Grasp.Shareability shareability, float dominance, bool overrideEquallyDominant, bool pacifying)
        {
            try
            {
                if (self is Player || self is Scavenger)
                {
                    if (obj is Scavenger)
                    {
                        pacifying = false;
                    }
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
            if (ModManager.DLCShared)
            {
                if (scav.scavenger.animation?.id == DLCSharedEnums.ScavengerAnimationID.Jumping)
                {
                    ParentalParams.getOrAdd(scav).wantCarryTimer = 0;
                    return false;
                }
            }
            if (scav.giftForMe != null)
            {
                ParentalParams.getOrAdd(scav).wantCarryTimer = 0;
                return false;
            }
            if (grabber.scavenger.Injured > 0f) return false;
            if ((scav.creature.abstractAI as ScavengerAbstractAI)!.GoHome()) return false;
            if (ControlledScavenger(scav.creature)) return false;
            if (!ScavengerParentTracker.map.TryGetValue(scav, out var parentTracker)) return false;
            if (scav.scared > 0.7) return true;
            if (grabber.agitation > 0.7) return true;
            if (parentTracker.unreachableCounter > 20) return true;
            if (ScavengerJunior_AbstractWantToBeHeld((ScavengerAbstractAI)scav.creature.abstractAI, (ScavengerAbstractAI)grabber.creature.abstractAI)) return true;
            return false;
        }
        
        bool ScavengerJunior_IsValidDrop(ScavengerAI scav, ScavengerAI grabber)
        {
            if (!scav.scavenger.room.aimap.TileAccessibleToCreature(grabber.creature.pos.x, grabber.creature.pos.y, scav.creature.creatureTemplate))
            {
                // please don't drop me into a pit
                return false;
            }

            if (scav.scavenger.room.aimap.getAItile(grabber.creature.pos).floorAltitude > 2)
            {
                // please drop me on the ground
                return false;
            }
            
            return true;
        }

        bool ScavengerJunior_AbstractWantToBeHeld(ScavengerAbstractAI scav, ScavengerAbstractAI grabber)
        {
            if (ControlledScavenger(scav.parent)) return false;
            // if (grabber.GoHome()) return true;
            if (scav.GoHome()) return false;
            if (scav.parent.Room.offScreenDen) return false;

            var currentRoom = scav.parent.Room;
            var attraction = currentRoom.AttractionForCreature(scav.parent);
            if (attraction == AbstractRoom.CreatureRoomAttraction.Avoid) return true;
            if (attraction == AbstractRoom.CreatureRoomAttraction.Forbidden) return true;
            var fearOfPredators = currentRoom.creatures
                .Except([scav.parent, grabber.parent])
                .Select(x => scav.parent.creatureTemplate.CreatureRelationship(x.creatureTemplate))
                .Select(x =>
                {
                    if (x.type == CreatureTemplate.Relationship.Type.Afraid) return x.intensity;
                    if (x.type == CreatureTemplate.Relationship.Type.Uncomfortable) return x.intensity * 0.5;
                    if (x.type == CreatureTemplate.Relationship.Type.SocialDependent) return x.intensity * 0.25;
                    return 0f;
                }).Sum();

            var roomfriendlyness = currentRoom.creatures
                .Except([scav.parent, grabber.parent])
                .Select(x => scav.parent.creatureTemplate.CreatureRelationship(x.creatureTemplate))
                .Select(x => x.type == CreatureTemplate.Relationship.Type.Pack ? x.intensity : 0f)
                .Sum();

            var scavSanctuary = currentRoom.scavengerOutpost || currentRoom.scavengerTrader;
            Logger.LogDebug("ABSTRACT WANT TO BE HELD");
            Logger.LogDebug(AbstractRoom.CreatureAttractionToFloat(attraction));
            Logger.LogDebug(scavSanctuary);
            Logger.LogDebug(roomfriendlyness);
            Logger.LogDebug(fearOfPredators);
            
            if (!scavSanctuary && (roomfriendlyness < 0.8f || fearOfPredators > 0.5f))
            {
                if (AbstractRoom.CreatureAttractionToFloat(attraction) < scav.parent.personality.nervous) return true;
                if (grabber.parent.personality.dominance > Mathf.Max(scav.parent.personality.dominance*1.5f, scav.parent.personality.dominance, 0)) return true;
            }
            
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

        void Player_GrabUpdateJuniorOnBack(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                cursor.Index = 0;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((Player self) =>
                {
                    self.GetJuniorOnBack().Update();
                });

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


                cursor.GotoNext(MoveType.After, x => x.MatchCall<Player>(nameof(Player.MaulingUpdate)));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((Player self) =>
                {
                    var onback = self.GetJuniorOnBack();
                    onback.increment = false;
                    onback.interactionLocked = true;
                });

                cursor.GotoNext(MoveType.After, x => x.MatchCall<Player>(nameof(Player.EatMeatUpdate)));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((Player self) =>
                {
                    var onback = self.GetJuniorOnBack();
                    onback.increment = false;
                    onback.interactionLocked = true;
                });

                cursor.GotoNext(MoveType.Before,
                    x => x.MatchBrtrue(out _),
                    x => x.MatchLdsfld<ModManager>(nameof(ModManager.CoopAvailable)),
                    x => x.Match(OpCodes.Brfalse),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<Player>(nameof(Player.wantToThrow)),
                    x => x.MatchLdcI4(0)
                );

                cursor.Emit(OpCodes.Ldarg, 0);
                cursor.Emit(OpCodes.Ldarg, 1);
                cursor.EmitDelegate((Player self, bool eu) =>
                {
                    var onback = self.GetJuniorOnBack();
                    if (self.wantToThrow > 0 && onback.scavenger != null)
                    {
                        onback.Throw(eu);
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
            Player.ObjectGrabability grabability = orig(self, obj);;
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

                if (ScavolutionPlugin.PlayerJunior != null && self.SlugCatClass == ScavolutionPlugin.PlayerJunior)
                {
                    if (grabability >= Player.ObjectGrabability.BigOneHand
                        && obj is not Creature) return Player.ObjectGrabability.CantGrab;
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            return grabability;
        }

        void JuniorOnBack_ScavengerAct(On.Scavenger.orig_Act orig, Scavenger self)
        {
            if (JuniorOnBack.onback_map.TryGetValue(self, out _))
            {
                self.movMode = SEScavengerMovementModes.OnBack;
                self.moveModeChangeCounter = 5;
            }

            if (self.movMode == SEScavengerMovementModes.OnBack)
            {
                if (self.animation != null)
                {
                    if (!self.animation.Continue)
                    {
                        self.animation = null;
                    }
                    else
                    {
                        self.animation.Update();
                    }
                }

                self.AI.Update();
                self.CombatUpdate();
                self.JumpLogicUpdate();

                Vector2 idealHeadPos = self.mainBodyChunk.pos + self.HeadLookDir * self.bodyChunkConnections[1].distance * 0.6f;
                self.bodyChunks[2].pos = Vector2.Lerp(self.bodyChunks[2].pos, idealHeadPos, 0.25f);

                if (--self.moveModeChangeCounter == 0)
                {
                    self.movMode = Scavenger.MovementMode.StandStill;
                }

                return;
            }
            else
            {
                orig(self);
            }
        }

        void ScavengerJunior_RunningUpdate(ILContext context)
        {
            try
            {
                ILCursor cursor = new ILCursor(context);
                /*
                    125	01BD	ldarg.0
                    126	01BE	ldfld	class Scavenger/MovementMode Scavenger::movMode
                    127	01C3	ldsfld	class Scavenger/MovementMode Scavenger/MovementMode::Climb
                    128	01C8	call	bool class ExtEnum`1<class Scavenger/MovementMode>::op_Equality(class ExtEnum`1<!0>, class ExtEnum`1<!0>)
                */

                ILLabel? outlabel = null;
                cursor.GotoNext(
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<Scavenger>(nameof(Scavenger.movMode)),
                    x => x.MatchLdsfld<Scavenger.MovementMode>(nameof(Scavenger.MovementMode.Climb)),
                    x => x.MatchCall<ExtEnum<Scavenger.MovementMode>>("op_Equality"),
                    x => x.MatchBrtrue(out outlabel)
                );

                if (outlabel is null) throw new InvalidOperationException("outlabel is null?");

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((Scavenger scav) =>
                {
                    return scav.movMode == SEScavengerMovementModes.OnBack;
                });
                cursor.Emit(OpCodes.Brtrue, outlabel);
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
    
    }
    

}