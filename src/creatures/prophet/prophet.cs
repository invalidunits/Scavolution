using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void RegisterScavengerProphet()
        {
            IL.ScavengerGraphics.ctor += ScavengerProphet_ScavengerGraphics_ctor;
        }

        void ScavengerProphet_ScavengerGraphics_ctor(ILContext context)
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
                    if (self.scavenger.isProphet())
                    {
                        self.cloak = new ScavengerCosmetic.TemplarCloak(self, spriteCount);
                        self.AddSubModule(self.cloak);
                        spriteCount += self.cloak.totalSprites;
                    }
                });

                cursor.GotoNext(MoveType.Before,
                    x => x.MatchLdarg(0), // 748	07DC	ldarg.0
                    x => x.MatchLdloc(1), // 749	07DD	ldloc.1
                    x => x.MatchStfld<ScavengerGraphics>("<MaskSprite>k__BackingField") // screw backing fields
                );

                cursor.MoveAfterLabels();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldloc_1);
                cursor.EmitDelegate((ScavengerGraphics self, int spriteCount) =>
                {
                    if (self.scavenger.isProphet())
                    {
                        self.maskGfx = new MoreSlugcats.VultureMaskGraphics(self.scavenger, VultureMask.MaskType.SCAVKING, spriteCount, "KingMask");
                        self.maskGfx.GenerateColor(self.scavenger.abstractCreature.ID.RandomSeed);
                    }
                });
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
    }

    static class ProphetExtensions
    {
        static public bool isProphet(this Scavenger scav)
        {
            if (SECreatureEnums.ScavengerProphet is null) return false;
            return scav.abstractCreature.creatureTemplate.type == SECreatureEnums.ScavengerProphet;
        }
    }
}
