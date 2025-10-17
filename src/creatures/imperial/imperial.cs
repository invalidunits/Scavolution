using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;
using UnityEngine;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void RegisterScavengerImperial()
        {
            PendulumHooks();
            // ImperialPathfindingHooks();
            IL.ScavengerGraphics.ctor += ScavengerImperial_ScavengerGraphics_ctor;
            IL.ScavengerCosmetic.TemplarCloak.DrawSprites += ScavengerImperial_TemplarCloak_DrawSprites;
            new Hook(typeof(Scavenger).GetProperty(nameof(Scavenger.Elite)).GetGetMethod(), ScavengerImperial_Elite);
            On.Scavenger.JumpLogicUpdate += Scavenger_JumpLogicUpdate;
            On.Scavenger.SetUpCombatSkills += ScavengerImperial_Scavenger_SetUpCombatSkills;

            On.ScavengerAbstractAI.InitGearUp += ScavengerImperial_AbstractScavengerAI_InitGearUP;
            On.ScavengerAbstractAI.ReGearInDen += ScavengerImperial_AbstractScavengerAI_ReGearInDen;
            On.ScavengerAI.ctor += ScavengerImperial_ScavengerAI_ctor;
            IL.Scavenger.FlyingWeapon += ScavengerImperial_FlyingWeapon_ctor;
        }
        
        public void ScavengerImperial_FlyingWeapon_ctor(ILContext context)
        {
            try
            {
                ILCursor cursor = new ILCursor(context);
                cursor.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt<ArtificialIntelligence>(nameof(ArtificialIntelligence.VisualContact)));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate((bool seeweapon, Scavenger scav, Weapon weapon) =>
                {
                    if (scav.isImperial() && scav.room.VisualContact(scav.mainBodyChunk.pos, weapon.firstChunk.pos))
                    {
                        if (weapon.thrownBy is not null && scav.room.VisualContact(scav.abstractCreature.pos, weapon.thrownBy.abstractCreature.pos))
                        {
                            scav.AI.tracker.CreatureNoticed(weapon.thrownBy.abstractCreature);
                        }
                        return true;
                    }

                    return seeweapon;
                });
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        
        public void ScavengerImperial_ScavengerAI_ctor(On.ScavengerAI.orig_ctor orig, ScavengerAI self, AbstractCreature creature, World world)
        {
            orig(self, creature, world);
            // if (SECreatureEnums.ScavengerImperial != null)
            // {
            //     if (creature.creatureTemplate.type == SECreatureEnums.ScavengerImperial)
            //     {
            //         self.AddModule(new SuperHearing(self, self.tracker, 350f * self.scavenger.reactionSkill*0.5f));
            //     }
            // }
            
        }

        void ScavengerImperial_Scavenger_SetUpCombatSkills(On.Scavenger.orig_SetUpCombatSkills orig, Scavenger self)
        {
            orig(self);
            if (self.isImperial())
            {

                self.midRangeSkill = Mathf.Max(1.0f, self.midRangeSkill);
                self.dodgeSkill = Mathf.Lerp(self.dodgeSkill, 1.0f, 0.7f);
                self.reactionSkill = Mathf.Lerp(self.reactionSkill, 1.0f, 0.5f);
            }
        }

        void ScavengerImperial_AbstractScavengerAI_InitGearUP(On.ScavengerAbstractAI.orig_InitGearUp orig, ScavengerAbstractAI self)
        {
            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerImperial)
                {
                    ScavImperialGearUp(self);
                    return;
                }
            }
            orig(self);
        }

        void ScavengerImperial_AbstractScavengerAI_ReGearInDen(On.ScavengerAbstractAI.orig_ReGearInDen orig, ScavengerAbstractAI self)
        {
            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerImperial)
                {
                    ScavImperialGearUp(self);
                    return;
                }
            }
            orig(self);
        }

        void ScavImperialGearUp(ScavengerAbstractAI self)
        {
            int restockOBJs = self.parent.creatureTemplate.grasps;
            List<int> forbiddengrasps = [];
            foreach (AbstractPhysicalObject.AbstractObjectStick stick in self.parent.stuckObjects)
            {
                if ((stick is AbstractPhysicalObject.CreatureGripStick gripstick) && (stick.A == self.parent))
                {
                    forbiddengrasps.Add(gripstick.grasp);
                }
            }

            List<(AbstractPhysicalObject.AbstractObjectType?, float)> itemweights = [
                ( null, 0.1f ),
                ( AbstractPhysicalObject.AbstractObjectType.Spear, 2.4f*(1.0f + self.parent.personality.aggression)),
                // ( AbstractPhysicalObject.AbstractObjectType.ScavengerBomb, 0.3f*(1.0f + self.parent.personality.aggression)),
                ( AbstractPhysicalObject.AbstractObjectType.Rock, 0.5f*(1.0f + self.parent.personality.dominance) ),
            ];

            if (ModManager.Watcher)
            {
                itemweights.Add((AbstractPhysicalObject.AbstractObjectType.GraffitiBomb, 0.6f*(1.0f + self.parent.personality.dominance)));
            }

            bool hasGraffiti = false;

            var state = UnityEngine.Random.state;
            float totalSum = itemweights.Select(x => x.Item2).Sum();
            int gaurenteedSpears = 2;

            for (int i = 0; i < restockOBJs; i++)
            {
                if (forbiddengrasps.Contains(i)) continue;
                float sum = totalSum;
                float value = sum * (UnityEngine.Random.Range(0, int.MaxValue) / (float)int.MaxValue); // use int range for max exclusivity
                foreach ((AbstractPhysicalObject.AbstractObjectType? itemtype, float weight) in itemweights.ToArray())
                {

                    value -= weight;
                    var item = itemtype;
                    if (ModManager.Watcher && hasGraffiti && item == AbstractPhysicalObject.AbstractObjectType.GraffitiBomb) continue;
                    if (gaurenteedSpears > 0)
                    {
                        gaurenteedSpears--;
                        item = AbstractPhysicalObject.AbstractObjectType.Spear;
                    }

                    if (value <= 0 || gaurenteedSpears > 0)
                    {
                        if (item == null) break;
                        AbstractPhysicalObject abstractPhysicalObject;
                        if (item == AbstractPhysicalObject.AbstractObjectType.Spear)
                        {
                            if (UnityEngine.Random.value < 0.2f)
                            {
                                abstractPhysicalObject = new AbstractSpear(self.world, null, self.parent.pos, self.world.game.GetNewID(), false, true);
                            }
                            else
                            {
                                if (ModManager.MSC)
                                {
                                    abstractPhysicalObject = new AbstractSpear(self.world, null, self.parent.pos, self.world.game.GetNewID(), false,
                                        Mathf.Lerp(0.35f, 0.6f, Custom.ClampedRandomVariation(0.5f, 0.5f, 2f)));
                                }
                                else
                                {
                                    abstractPhysicalObject = new AbstractSpear(self.world, null, self.parent.pos, self.world.game.GetNewID(), true);
                                }
                            }
                        }
                        else if (ModManager.Watcher && item == AbstractPhysicalObject.AbstractObjectType.GraffitiBomb)
                        {
                            hasGraffiti = true;
                            abstractPhysicalObject = new AbstractConsumable(self.world, item, null, self.parent.pos, self.world.game.GetNewID(), -1, -1, null);
                        }
                        else
                        {
                            abstractPhysicalObject = new AbstractPhysicalObject(self.world, item, null, self.parent.pos, self.world.game.GetNewID());
                        }

                        self.world.GetAbstractRoom(self.parent.pos).AddEntity(abstractPhysicalObject);
                        new AbstractPhysicalObject.CreatureGripStick(self.parent, abstractPhysicalObject, i, true);
                        break;
                    }
                }
            }

            UnityEngine.Random.state = state;
        }

        public void Scavenger_JumpLogicUpdate(On.Scavenger.orig_JumpLogicUpdate orig, Scavenger self)
        {
            if (self.isImperial())
            {
                // need custom jumping logic for this
                return;
            }
            orig(self);
        }

        bool ScavengerImperial_Elite(Func<Scavenger, bool> orig, Scavenger self)
        {
            if (self.isImperial()) return true;
            return orig(self);
        }

        void ScavengerImperial_TemplarCloak_DrawSprites(ILContext ctx)
        {
            try
            {
                ILCursor cursor = new(ctx);
                cursor.GotoNext(MoveType.After, x => x.MatchCallOrCallvirt(typeof(RainWorld).GetProperty(nameof(RainWorld.GoldRGB)).GetGetMethod()));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((Color c, ScavengerCosmetic.TemplarCloak cloak) =>
                {
                    if (cloak.scavGrphs.scavenger.isImperial())
                    {
                        return cloak.blackColor;
                    }

                    return c;
                });
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            } 
        }

        void ScavengerImperial_ScavengerGraphics_ctor(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);

                cursor.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0), // 748	07DC	ldarg.0
                    x => x.MatchLdloc(1), // 749	07DD	ldloc.1
                    x => x.MatchStfld<ScavengerGraphics>("<FirstBehindLimbSprite>k__BackingField") // screw backing fields
                );

                cursor.MoveAfterLabels();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloca, 1);
                cursor.EmitDelegate((ScavengerGraphics self, ref int spriteCount) =>
                {
                    if (self.scavenger.isImperial())
                    {
                        self.cloak = new ScavengerCosmetic.TemplarCloak(self, spriteCount);
                        self.AddSubModule(self.cloak);
                        spriteCount += self.cloak.totalSprites;
                    }
                });
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
    }

    static class ImperialExtensions
    {
        static public bool isImperial(this Scavenger scav)
        {
            if (SECreatureEnums.ScavengerImperial is null) return false;
            return scav.abstractCreature.creatureTemplate.type == SECreatureEnums.ScavengerImperial;
        }
    }
}
