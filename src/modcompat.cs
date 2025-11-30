using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Scavolution 
{
    public partial class ScavolutionPlugin
    {
        static public bool NotSlugcatPlayables { get; private set; } = false;
        static public bool M4rblelousEntityPack { get; private set; } = false;
        static public bool SlugBase {get; private set; } = false;
        static public SlugcatStats.Name? PlayerImperial { get; private set; } = null;
        static public SlugcatStats.Name? PlayerJunior { get; private set; } = null;
        static public void InitializeModCompatibility()
        {
            NotSlugcatPlayables = ModManager.ActiveMods.Any(x => x.id == "sprobgik.desecratinggraves");
            if (NotSlugcatPlayables) ScavolutionPlugin.pubLogger?.LogDebug("not playable slugcats has been enabled!");
            M4rblelousEntityPack = ModManager.ActiveMods.Any(x => x.id == "lb-fgf-m4r-ik.modpack");
            if (M4rblelousEntityPack) ScavolutionPlugin.pubLogger?.LogDebug("The M4rblelous Entity Pack has been enabled!");
            SlugBase = ModManager.ActiveMods.Any(x => x.id == "slime-cubed.slugbase");
            if (SlugBase) 
            {
                ScavolutionPlugin.pubLogger?.LogDebug("SlugBase has been enabled!");
                PlayerImperial = new global::SlugcatStats.Name("PlayerImperial", false);
                PlayerJunior = new global::SlugcatStats.Name("PlayerJunior", false);
            }

            
            if (NotSlugcatPlayables)
            {
                AddImperialPlayerHooks();
            }

            if (SlugBase)
            {
                nsp_detours.Add(new Hook(typeof(SlugcatStats).GetMethod(nameof(SlugcatStats.HiddenOrUnplayableSlugcat)), ImperialPlayer_SlugcatStats_HiddenOrUnplayableSlugcat));
            }
            
            foreach (IDetour detour in nsp_detours)
            {
                detour.Apply();
            }
        }

        public static bool ImperialPlayer_SlugcatStats_HiddenOrUnplayableSlugcat(On.SlugcatStats.orig_HiddenOrUnplayableSlugcat orig, global::SlugcatStats.Name i)
        {
            if (i == PlayerImperial && (!NotSlugcatPlayables || SECreatureEnums.ScavengerImperial == null)) return true;
            return orig(i);
        }

        static public List<IDetour> nsp_detours = [];
        static public void AddImperialPlayerHooks()
        {
            if (!nsp_detours.Any())
            {
                nsp_detours.AddRange([
                    new Hook(typeof(SprobDesecratingGraves.Main).GetMethod(nameof(SprobDesecratingGraves.Main.isScavPlayer), [typeof(SlugcatStats.Name)]), SprobDesecratingGraves_isScavPlayer_SlugcatStatsName),
                    new Hook(typeof(SprobDesecratingGraves.Main).GetMethod(nameof(SprobDesecratingGraves.Main.isScavPlayer), [typeof(Player)]), SprobDesecratingGraves_isScavPlayer_Player),
                ]);
            }
            
        }
        static public bool SprobDesecratingGraves_isScavPlayer_SlugcatStatsName(Func<SlugcatStats.Name, bool> orig, SlugcatStats.Name name)
        {
            if (name == PlayerImperial) return true;
            if (name == PlayerJunior) return true;
            return orig(name);
        }

        static public bool SprobDesecratingGraves_isScavPlayer_Player(Func<Player, bool> orig, Player player)
        {
            if (player.SlugCatClass == PlayerImperial) return true;
            if (player.SlugCatClass == PlayerJunior) return true;
            return orig(player);
        }

        static public bool ControlledScavenger(AbstractCreature scav)
        {
            return (ModManager.MSC && scav.controlled) || (NotSlugcatPlayables && NotSlugcatPlayableScavengerImpl(scav));
        }

        static public bool NotSlugcatPlayableScavengerImpl(AbstractCreature scav)
        {
            if (NotSlugcatPlayables)
            {
                return SprobDesecratingGraves.ScavengerHooks.GetScavengerData(scav).controller != null;
            }
            return false;
        }
    }
}
