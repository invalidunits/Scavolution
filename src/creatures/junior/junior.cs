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
using System.Text.RegularExpressions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using UnityEngine;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void RegisterScavengerJunior()
        {
            Logger.LogDebug("Registering scav junior");

            // registered stuff
                On.StaticWorld.InitCustomTemplates += StaticWorld_InitCustomTemplates;
                On.CreatureTemplate.ctor_Type_CreatureTemplate_List1_List1_Relationship += CreatureTemplate_ctor;
                On.AbstractCreature.ctor += AbstractCreature_ctor;

            // gameplay stuff
                JuniorAIHooks();
                JuniorOnBackHooks();

                // Gear
                On.ScavengerAbstractAI.InitGearUp += AbstractScavengerAI_InitGearUP;
                On.ScavengerAbstractAI.ReGearInDen += AbstractScavengerAI_ReGearInDen;

                // Weight
                IL.Scavenger.Update += Scanger_UpdateScavengerJumpJunior;
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


        void Scanger_UpdateScavengerJumpJunior(ILContext context)
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
            for (int i = 0; i < 2; i++)
            {
                if (UnityEngine.Random.value < 0.4) continue;

                if (UnityEngine.Random.value < 0.6)
                {
                    AbstractPhysicalObject abstractPhysicalObject = new AbstractPhysicalObject(self.world, AbstractPhysicalObject.AbstractObjectType.ScavengerBomb, null, self.parent.pos, self.world.game.GetNewID());
                    self.world.GetAbstractRoom(self.parent.pos).AddEntity(abstractPhysicalObject);
                    new AbstractPhysicalObject.CreatureGripStick(self.parent, abstractPhysicalObject, i, true);
                }
                else if (ModManager.Watcher && UnityEngine.Random.value < 0.7)
                {
                    AbstractPhysicalObject abstractPhysicalObject = new AbstractPhysicalObject(self.world, Watcher.WatcherEnums.AbstractObjectType.Boomerang, null, self.parent.pos, self.world.game.GetNewID());
                    self.world.GetAbstractRoom(self.parent.pos).AddEntity(abstractPhysicalObject);
                    new AbstractPhysicalObject.CreatureGripStick(self.parent, abstractPhysicalObject, i, true);
                }
                else
                {
                    AbstractPhysicalObject abstractPhysicalObject = new AbstractPhysicalObject(self.world, AbstractPhysicalObject.AbstractObjectType.Rock, null, self.parent.pos, self.world.game.GetNewID());
                    self.world.GetAbstractRoom(self.parent.pos).AddEntity(abstractPhysicalObject);
                    new AbstractPhysicalObject.CreatureGripStick(self.parent, abstractPhysicalObject, i, true);
                }
            }
        }
        void AbstractScavengerAI_InitGearUP(On.ScavengerAbstractAI.orig_InitGearUp orig, ScavengerAbstractAI self)
        {
            if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                ScavJuniorGearUp(self);
                return;
            }
            orig(self);
        }

        void AbstractScavengerAI_ReGearInDen(On.ScavengerAbstractAI.orig_ReGearInDen orig, ScavengerAbstractAI self)
        {
            if (self.parent.creatureTemplate.type == SECreatureEnums.ScavengerJunior)
            {
                ScavJuniorGearUp(self);
                return;
            }
            orig(self);
        }

        public class JuniorState : HealthState
        {
            public JuniorState(AbstractCreature creature) : base(creature)
            {
                currentParent = null;
            }

            public int cyclesSinceSeenParent = 0;
            public int? currentParent;
            const string currentParentSaveID = "ScavolutionJuniorParent";
            public override string ToString()
            {
                string text = base.ToString();
                if (currentParent.HasValue)
                {
                    text += string.Format(CultureInfo.InvariantCulture, "<cB>{0}<cC>{1}", currentParentSaveID, currentParent.Value.ToString());
                }
                return text;
            }

            public override void LoadFromString(string[] s)
            {
                currentParent = null;
                for (int i = 0; i < s.Length; i++)
                {
                    string text = Regex.Split(s[i], "<cC>")[0];
                    if (text != null && text == currentParentSaveID)
                    {
                        currentParent = int.Parse(Regex.Split(s[i], "<cC>")[1]);
                    }
                }

                unrecognizedSaveStrings.Remove(currentParentSaveID);
                base.LoadFromString(s);
            }

            public override void CycleTick()
            {
                base.CycleTick();
                if ((cyclesSinceSeenParent++) >= 2)
                {
                    currentParent = null;
                }
            }
        }


        void AbstractCreature_ctor(On.AbstractCreature.orig_ctor orig, AbstractCreature self, World world, CreatureTemplate creatureTemplate, Creature realizedCreature, WorldCoordinate pos, EntityID ID)
        {
            orig(self, world, creatureTemplate, realizedCreature, pos, ID);
            try
            {
                if (creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    self.abstractAI = new ScavengerAbstractAI(self.world, self);
                    self.state = new JuniorState(self);
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }



        CreatureTemplate? ScavengerJuniorTemplate = null;

        public void StaticWorld_InitCustomTemplates(On.StaticWorld.orig_InitCustomTemplates orig)
        {
            Logger.LogDebug("Initializing Scavenger Junior");
            orig();

            Logger.LogDebug(new StackTrace().ToString());
            if (ScavengerJuniorTemplate == null)
            {
                List<TileTypeResistance> tile_resistance = new List<TileTypeResistance>();
                List<TileConnectionResistance> tile_connection_resistance = new List<TileConnectionResistance>();
                ScavengerJuniorTemplate = new CreatureTemplate(SECreatureEnums.ScavengerJunior,
                    StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger),
                    tile_resistance,
                    tile_connection_resistance,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Uncomfortable, 0.2f)
                );
                ScavengerJuniorTemplate.BlizzardWanderer = false;
                ScavengerJuniorTemplate.BlizzardAdapted = false;
                ScavengerJuniorTemplate.baseDamageResistance = 1.5f;
                ScavengerJuniorTemplate.baseStunResistance = 0.8f;
                ScavengerJuniorTemplate.instantDeathDamageLimit = 1.0f;

                ScavengerJuniorTemplate.offScreenSpeed = 1.25f;
                ScavengerJuniorTemplate.grasps = 2;
                ScavengerJuniorTemplate.AI = true;
                ScavengerJuniorTemplate.requireAImap = true;
                ScavengerJuniorTemplate.abstractedLaziness = 50;
                ScavengerJuniorTemplate.bodySize = 0.8f;
                ScavengerJuniorTemplate.doPreBakedPathing = false;
                ScavengerJuniorTemplate.preBakedPathingAncestor = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.StandardGroundCreature);
                ScavengerJuniorTemplate.stowFoodInDen = false;
                ScavengerJuniorTemplate.shortcutSegments = 1;

                ScavengerJuniorTemplate.visualRadius = 1000f;
                ScavengerJuniorTemplate.movementBasedVision = 0.3f;

                ScavengerJuniorTemplate.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
                ScavengerJuniorTemplate.hibernateOffScreen = true;
                ScavengerJuniorTemplate.roamBetweenRoomsChance = -1f;
                ScavengerJuniorTemplate.roamInRoomChance = -1f;
                ScavengerJuniorTemplate.socialMemory = true;
                ScavengerJuniorTemplate.communityID = CreatureCommunities.CommunityID.Scavengers;
                ScavengerJuniorTemplate.communityInfluence = 2f;
                ScavengerJuniorTemplate.dangerousToPlayer = 0.1f;

                ScavengerJuniorTemplate.meatPoints = 2;
                ScavengerJuniorTemplate.usesNPCTransportation = true;
                ScavengerJuniorTemplate.usesRegionTransportation = true;
                ScavengerJuniorTemplate.usesCreatureHoles = false;
                ScavengerJuniorTemplate.jumpAction = "Jump";
                ScavengerJuniorTemplate.pickupAction = "Pick Up";
                ScavengerJuniorTemplate.throwAction = "Throw";
            }

            for (int i = 0; i < StaticWorld.creatureTemplates.Length; i++)
            {
                if (StaticWorld.creatureTemplates[i] == null)
                {
                    StaticWorld.creatureTemplates[i] = ScavengerJuniorTemplate;
                }
            }
        }

        public void CreatureTemplate_ctor(On.CreatureTemplate.orig_ctor_Type_CreatureTemplate_List1_List1_Relationship orig, global::CreatureTemplate self, global::CreatureTemplate.Type type, global::CreatureTemplate ancestor, List<global::TileTypeResistance> tileResistances, List<global::TileConnectionResistance> connectionResistances, global::CreatureTemplate.Relationship defaultRelationship)
        {
            orig(self, type, ancestor, tileResistances, connectionResistances, defaultRelationship);
            try
            {
                if (self.type == SECreatureEnums.ScavengerJunior)
                {
                    self.name = "ScavengerJunior";
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
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
            return scav.abstractCreature.creatureTemplate.type == SECreatureEnums.ScavengerJunior;
        }
    }
}