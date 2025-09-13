using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void RegisterScavengerImperial()
        {
            ImperialPathfindingHooks();
            IL.ScavengerGraphics.ctor += ScavengerImperial_ScavengerGraphics_ctor;
            new Hook(typeof(Scavenger).GetProperty(nameof(Scavenger.Elite)).GetGetMethod(), ScavengerImperial_Elite);
            On.Scavenger.JumpLogicUpdate += Scavenger_JumpLogicUpdate;
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
