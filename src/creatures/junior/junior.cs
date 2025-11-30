// make junior smaller and visually distinct.
// adjustments to personality?
// animation fixing


// remove from scavenger squads.
// change personality


// make carryable?
// make backpackable by both scavengers and players.
// scavengers will backpack children when children are tired / they cannot navigate the area / the area is generally dangerous.
// social events? grabbing without cause or reputation makes scavs angry.


// scavenger juniors will try to keep up with a squad even if they aren't formally apart of one

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using SprobDesecratingGraves;
using UnityEngine;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void RegisterScavengerJunior()
        {
            // state
            On.HealthState.ctor += HealthState_ctor;
            On.HealthState.ToString += HealthState_ToString;
            On.HealthState.LoadFromString += HealthState_LoadFromString;
            On.HealthState.CycleTick += HealthState_CycleTick;

            
            // gameplay stuff
            JuniorAIHooks();
            JuniorOnBackHooks();

            // Gear
            On.ScavengerAbstractAI.InitGearUp += ScavengerJunior_AbstractScavengerAI_InitGearUP;
            On.ScavengerAbstractAI.ReGearInDen += ScavengerJunior_AbstractScavengerAI_ReGearInDen;

            // Jumping
            IL.Scavenger.Update += Scavenger_UpdateScavengerJumpJunior;

            // Nerf Jumping
            IL.Scavenger.Jump += ScavengerJunior_Scavenger_Jump;
            IL.Scavenger.JumpFinder.NewTest += ScavengerJunior_Scavenger_JumpFinder_NewTest;

            // Weight
            IL.Scavenger.Update += Scanvenger_UpdateJuniorMass;

            // graphical stuff
            IL.ScavengerGraphics.ctor += ScavengerGraphics_ctorJunior;
            IL.ScavengerGraphics.ScavengerHand.DrawSprites_SpriteLeaser_RoomCamera_float_float2 += ScavengerHand_DrawSprites;
            On.ScavengerGraphics.ScavengerLeg.ctor += ScavengerLeg_ctor;
            On.ScavengerGraphics.ScavengerHand.ctor += ScavengerHand_ctor;
            On.ScavengerGraphics.ctor += ScavengerGraphics_ctor;
            On.ScavengerGraphics.DrawSprites += ScavengerGraphics_DrawSprites;
            On.Scavenger.ctor += Scavenger_ctor;
        }

        void ScavengerJunior_Scavenger_Jump(ILContext context)
        {
            /*
            134	0182	ldarg.0
            135	0183	ldc.i4.s	20
            136	0185	stfld	int32 Scavenger::addDelay
            */
            try
            {
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchStfld<Scavenger>(nameof(Scavenger.addDelay))
                );
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((int addDelay, Scavenger self) =>
                {
                    if (self.isJunior())
                    {
                        self.jumpFinders.Clear();
                        foreach (Creature.Grasp grasp in self.grabbedBy)
                        {
                            if (grasp.grabber is Player || grasp.grabber is Scavenger)
                            {
                                grasp.Release();
                            }
                        }


                        return Math.Max(addDelay, 60);
                    }                    
                    return addDelay;
                });

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        void ScavengerJunior_Scavenger_JumpFinder_NewTest(ILContext context)
        {
            /*
            21	0042	ldc.r4	14
            22	0047	ldc.r4	50
            23	004C	ldloc.0
            24	004D	call	float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Lerp(float32, float32, float32)
            25	0052	stloc.1
            */
            try
            {
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchStloc(1)
                );
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((float jumpforce, Scavenger self) =>
                {
                    return jumpforce*0.4f;
                });

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        void Scavenger_UpdateScavengerJumpJunior(ILContext context)
        {
            /*
                483	05D5	call	instance bool Scavenger::get_Elite()
                484	05DA	brtrue.s	488 (05E4) ldarg.0 
                485	05DC	ldarg.0
                486	05DD	call	instance bool Scavenger::get_Templar()
                487	05E2	brfalse.s	490 (05EA) ldarg.0 
                488	05E4	ldarg.0
                489	05E5	call	instance void Scavenger::JumpLogicUpdate()
            */

            try
            {
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchCall(typeof(Scavenger).GetProperty(nameof(Scavenger.Elite)).GetGetMethod()),
                    x => x.MatchBrtrue(out _),
                    x => x.MatchLdarg(0),
                    x => x.MatchCall(typeof(Scavenger).GetProperty(nameof(Scavenger.Templar)).GetGetMethod()),
                    x => x.MatchBrfalse(out _),
                    x => x.MatchLdarg(0),
                    x => x.MatchCall<Scavenger>(nameof(Scavenger.JumpLogicUpdate))
                );

                cursor.Index += 1;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate((bool canJump, Scavenger scav) => canJump || scav.isJunior());

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        void ScavengerGraphics_ctorJunior(ILContext context)
        {
            try
            {
                /*
    404	03C6	ldarg.0
405	03C7	ldloc.1
406	03C8	dup
407	03C9	ldc.i4.1
408	03CA	add
409	03CB	stloc.1
410	03CC	stfld	int32 ScavengerGraphics::'<HeadSprite>k__BackingField'

                */
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdloc(1),
                    x => x.MatchDup(),
                    x => x.MatchLdcI4(1),
                    x => x.MatchAdd(),
                    x => x.MatchStloc(1),
                    x => x.MatchStfld<ScavengerGraphics>("<HeadSprite>k__BackingField")
                );

                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloca, 1);
                cursor.EmitDelegate((ScavengerGraphics self, ref int spritenum) =>
                {
                    if (self.scavenger.isJunior())
                    {
                        var scarf = new JuniorScarf(self, spritenum);
                        self.AddSubModule(scarf);
                        spritenum += scarf.totalSprites;
                    }

                });

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

        }

        void ScavJuniorGearUp(ScavengerAbstractAI self)
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
                ( null, 0.3f ),
                ( AbstractPhysicalObject.AbstractObjectType.ScavengerBomb, 0.4f ),
                ( AbstractPhysicalObject.AbstractObjectType.DataPearl, 0.1f ),
                ( AbstractPhysicalObject.AbstractObjectType.Rock, 0.5f ),
            ];

            if (ModManager.Watcher)
            {
                itemweights.Add((Watcher.WatcherEnums.AbstractObjectType.Boomerang, 0.3f));
            }


            float totalSum = itemweights.Select(x => x.Item2).Sum();
            var state = UnityEngine.Random.state;
            for (int i = 0; i < restockOBJs; i++)
            {
                if (forbiddengrasps.Contains(i)) continue;
                float sum = totalSum;
                float value = sum * (UnityEngine.Random.Range(0, int.MaxValue) / (float)int.MaxValue); // use int range for max exclusivity
                foreach ((AbstractPhysicalObject.AbstractObjectType? itemtype, float weight) in itemweights.ToArray())
                {
                    value -= weight;
                    if (value <= 0)
                    {
                        if (itemtype == null) break;
                        AbstractPhysicalObject abstractPhysicalObject;
                        if (itemtype == AbstractPhysicalObject.AbstractObjectType.DataPearl)
                        {
                            // TODO: junior lore pearl
                            abstractPhysicalObject = new DataPearl.AbstractDataPearl(self.world,
                                AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
                                self.parent.pos, self.world.game.GetNewID(), -1, -1, null,
                                DataPearl.AbstractDataPearl.DataPearlType.Misc);
                        }
                        else if (itemtype == AbstractPhysicalObject.AbstractObjectType.GraffitiBomb)
                        {
                            abstractPhysicalObject = new GraffitiBomb.AbstractGraffitiBomb(self.world, null, self.parent.pos, self.world.game.GetNewID(), -1, -1, null);
                        }
                        else
                        {
                            abstractPhysicalObject = new AbstractPhysicalObject(self.world, itemtype, null, self.parent.pos, self.world.game.GetNewID());
                        }
                        self.world.GetAbstractRoom(self.parent.pos).AddEntity(abstractPhysicalObject);
                        new AbstractPhysicalObject.CreatureGripStick(self.parent, abstractPhysicalObject, i, true);
                        break;
                    }
                }
            }
            UnityEngine.Random.state = state;
        }
        void ScavengerJunior_AbstractScavengerAI_InitGearUP(On.ScavengerAbstractAI.orig_InitGearUp orig, ScavengerAbstractAI self)
        {
            if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                ScavJuniorGearUp(self);
                return;
            }
            orig(self);
        }

        void ScavengerJunior_AbstractScavengerAI_ReGearInDen(On.ScavengerAbstractAI.orig_ReGearInDen orig, ScavengerAbstractAI self)
        {
            if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                ScavJuniorGearUp(self);
                return;
            }
            orig(self);
        }

        void HealthState_ctor(On.HealthState.orig_ctor orig, HealthState self, AbstractCreature creature)
        {
            orig(self, creature);

            if (creature.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                new JuniorState(self);
            }
        }

        void HealthState_LoadFromString(On.HealthState.orig_LoadFromString orig, HealthState self, string[] s)
        {
            orig(self, s);
            if (JuniorState.map.TryGetValue(self, out var juniorstate)) juniorstate.LoadFromString(s);
        }

        string HealthState_ToString(On.HealthState.orig_ToString orig, HealthState self)
        {
            string ret = orig(self);
            if (JuniorState.map.TryGetValue(self, out var juniorstate)) juniorstate.Save(ref ret);
            return ret;
        }

        void HealthState_CycleTick(On.HealthState.orig_CycleTick orig, HealthState self)
        {
            orig(self);
            if (JuniorState.map.TryGetValue(self, out var juniorstate)) juniorstate.CycleTick();
        }


        public class JuniorState
        {
            CreatureState state;
            public static ConditionalWeakTable<CreatureState, JuniorState> map = new();
            public JuniorState(CreatureState state)
            {
                this.state = state;
                currentParent = null;
                map.Add(state, this);
            }

            public int cyclesSinceSeenParent = 0;
            public int? currentParent;
            const string currentParentSaveID = "ScavolutionJuniorParent";
            const string cyclesSinceSeenParentSaveID = "ScavolutionJuniorCycleOrphan";
            public void Save(ref string text)
            {
                if (currentParent.HasValue)
                {
                    text += $"<cB>{currentParentSaveID}<cC>{currentParent.Value}";
                    text += $"<cB>{cyclesSinceSeenParentSaveID}<cC>{cyclesSinceSeenParent}";
                }

                // pubLogger?.LogDebug(text);
            }

            public void LoadFromString(string[] s)
            {
                currentParent = null;
                for (int i = 0; i < s.Length; i++)
                {
                    var joarxml = Regex.Split(s[i], "<cC>");
                    string text = joarxml[0];
                    if (text != null && text == currentParentSaveID)
                    {
                        currentParent = int.Parse(joarxml[1]);
                        state.unrecognizedSaveStrings.Remove(currentParentSaveID);
                    }

                    if (text != null && text == cyclesSinceSeenParentSaveID)
                    {
                        cyclesSinceSeenParent = int.Parse(joarxml[1]);
                        state.unrecognizedSaveStrings.Remove(cyclesSinceSeenParentSaveID);
                    }
                }


                ScavolutionPlugin.pubLogger?.LogDebug("junior state loaded");
                ScavolutionPlugin.pubLogger?.LogDebug(state.creature);
                ScavolutionPlugin.pubLogger?.LogDebug(currentParent);
            }

            public void CycleTick()
            {
                if ((cyclesSinceSeenParent++) >= 2)
                {
                    currentParent = null;
                }
            }
        }

        public void ScavengerLeg_ctor(On.ScavengerGraphics.ScavengerLeg.orig_ctor orig, ScavengerGraphics.ScavengerLeg self, ScavengerGraphics owner, int num, int firstSprite)
        {
            try
            {
                if (owner.scavenger.isJunior())
                {
                    self.legLength /= 2.0f;
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            orig(self, owner, num, firstSprite);
        }

        public void ScavengerHand_ctor(On.ScavengerGraphics.ScavengerHand.orig_ctor orig, ScavengerGraphics.ScavengerHand self, ScavengerGraphics owner, int num, int firstSprite)
        {
            try
            {
                if (owner.scavenger.isJunior())
                {
                    self.armLength /= 1.5f;
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
            orig(self, owner, num, firstSprite);
        }

        public void ScavengerHand_DrawSprites(ILContext context)
        {
            try
            {
                var transform_length = (float len, ScavengerGraphics.ScavengerHand graphics) =>
                {
                    try
                    {
                        if (graphics.scavenger.isJunior())
                        {
                            return len / 1.5f;
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.LogError(except);
                    }

                    return len;
                };
                /*
                    258	033E	ldc.r4	18
                    259	0343	ldc.r4	18
                    260	0348	ldarg.0
                    261	0349	ldarg.3
                    262	034A	call	instance float32 ScavengerGraphics/ScavengerHand::MyFlip(float32)
                    263	034F	call	valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 RWCustom.Custom::InverseKinematic(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32, float32)

                */
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.Before,
                    x => x.Match(OpCodes.Ldc_R4),
                    x => x.Match(OpCodes.Ldc_R4),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdarg(3),
                    x => x.MatchCall<ScavengerGraphics.ScavengerHand>(nameof(ScavengerGraphics.ScavengerHand.MyFlip)),
                    x => x.MatchCall(typeof(Custom).GetMethod(nameof(Custom.InverseKinematic)))
                );

                for (int i = 0; i < 2; i++)
                {
                    cursor.GotoNext(MoveType.After, x => x.Match(OpCodes.Ldc_R4));
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate(transform_length);
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void ScavengerGraphics_ctor(On.ScavengerGraphics.orig_ctor orig, ScavengerGraphics self, PhysicalObject ow)
        {
            orig(self, ow);
            try
            {
                if (self.scavenger.isJunior())
                {

                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void ScavengerGraphics_DrawSprites(On.ScavengerGraphics.orig_DrawSprites orig, ScavengerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPosV2)
        {
            orig(self, sLeaser, rCam, timeStacker, camPosV2);
            try
            {
                if (self.scavenger.isJunior())
                {
                    sLeaser.sprites[self.HeadSprite].scaleX /= 1.3f;
                    sLeaser.sprites[self.HeadSprite].scaleY /= 1.3f;
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void Scanvenger_UpdateJuniorMass(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                ILLabel exitlabel = null!;
                cursor.GotoNext(MoveType.After,
                    x => x.MatchBr(out exitlabel),
                    x => x.MatchLdcR4(out _),
                    x => x.MatchStloc(0));
                cursor.Goto(exitlabel.Target, MoveType.Before);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate(FixScavengerJuniorMass);
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public void FixScavengerJuniorMass(Scavenger scav)
        {
            if (scav.isJunior())
            {
                scav.bodyChunks[0].mass /= 1.3f;
                scav.bodyChunks[1].mass /= 1.3f;
                scav.bodyChunks[2].mass /= 1.3f;
            }
        }

        public void Scavenger_ctor(On.Scavenger.orig_ctor orig, Scavenger self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            try
            {
                if (self.isJunior())
                {
                    self.bodyChunks[0].rad /= 2.3f;
                    self.bodyChunks[1].rad /= 2.3f;
                    self.bodyChunks[2].rad /= 1.5f;
                    self.bodyChunkConnections[0].distance /= 2.3f;
                    self.bodyChunkConnections[1].distance /= 2.3f;
                    FixScavengerJuniorMass(self);
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }


    }
    

    static class JuniorExtensions
    {
        static public bool isJunior(this Scavenger scav)
        {
            if (ScavolutionPlugin.NotSlugcatPlayables)
            {
                if (isJunior_NSP(scav)) return true;
            }

            return scav.abstractCreature.creatureTemplate.type == SECreatureEnums.ScavengerJunior;
        }

        static private bool isJunior_NSP(Scavenger scav)
        {
            var data = scav.abstractCreature.GetScavengerData();
            return data?.controller is not null && data.controller.SlugCatClass == ScavolutionPlugin.PlayerJunior;
        }
    }
}