using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
                this.counter++;
                if (this.counter > 20)
                {
                    if (this.scavenger != null)
                    {
                        this.ScavtoHand();
                        this.counter = 0;
                    }
                    else if (this.scavenger == null)
                    {
                        for (int i = 0; i < 2; i++)
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


            if (owner is Player p)
            {
                var restpos = (owner.graphicsModule is PlayerGraphics playerGraphics) ? playerGraphics.head.pos : owner.mainBodyChunk.pos;
                restpos += new Vector2(0, 14f);

                scavenger.flip = Mathf.Lerp(scavenger.flip, p.flipDirection, 0.8f);

                var offset = restpos - scavenger.bodyChunks[0].pos;
                scavenger.bodyChunks[0].RelativeMoveFromOutsideMyUpdate(eu, offset);
                scavenger.bodyChunks[1].RelativeMoveFromOutsideMyUpdate(eu, offset);
                scavenger.bodyChunks[2].RelativeMoveFromOutsideMyUpdate(eu, offset);

                scavenger.bodyChunks[0].vel = owner.mainBodyChunk.vel; // torsoe
                scavenger.bodyChunks[1].vel = Vector2.Lerp(scavenger.bodyChunks[1].vel, owner.mainBodyChunk.vel, 0.5f); // legs
                // no vel sync for head

                
                if (ModManager.DLCShared)
                {
                    if (scavenger.animation != null)
                    {
                        if (scavenger.animation.id != DLCSharedEnums.ScavengerAnimationID.Jumping)
                            scavenger.animation = new Scavenger.JumpingAnimation(scavenger);
                    }
                }

                scavenger.movMode = Scavenger.MovementMode.StandStill;
            }

            if (owner is Scavenger)
            {
                // TODO: scav on scav action
            }
        }
        public void ScavtoHand()
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

            On.Player.Grabability += Player_GrababilityJunior;
            On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdatedJuniorOnBack;
            IL.Player.GrabUpdate += Player_GrabUpdateJuniorOnBack;
            On.Player.GrabUpdate += Player_UpdateJuniorOnBack;
            On.Player.CanIPickThisUp += Player_CanIPickThisUpJunior;
            On.Player.Destroy += Player_DestroyJuniorOnBack;

            On.Scavenger.RecreateSticksFromAbstract += JuniorOnBack_Scavenger_RecreateSticksFromAbstract;
            On.Scavenger.RecreateSticksFromAbstract += JuniorOnBack_Scavenger_RecreateSticksFromAbstract;
        }

        bool ScangerJunior_WantToPiggyBack(ScavengerAI scav)
        {
            if (!ScavengerParentTracker.map.TryGetValue(scav, out var parentTracker)) return false;
            if (parentTracker.tiredness > 20) return true;
            if (scav.threatTracker.Utility() > 0.5f) return true;
            return false;
        }

        void JuniorOnBack_Scavenger_RecreateSticksFromAbstract(On.Scavenger.orig_RecreateSticksFromAbstract orig, Scavenger self)
        {
            orig(self);
            try
            {
                foreach (AbstractPhysicalObject.AbstractObjectStick stick in self.abstractCreature.stuckObjects)
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

        void Player_DestroyJuniorOnBack(On.Player.orig_Destroy orig, Player self)
        {
            orig(self);
            try
            {
                self.GetJuniorOnBack().ChangeOverlap(true);
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
                    if (grasp.grabber is Scavenger scav2 && scav2.isJunior()) return Player.ObjectGrabability.CantGrab;
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