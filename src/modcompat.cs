using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;

namespace Scavolution 
{
    public partial class ScavolutionPlugin
    {
        static public bool NotSlugcatPlayables { get; private set; } = false;
        static public bool M4rblelousEntityPack { get; private set; } = false;
        static void InitializeModCompatibility()
        {
            NotSlugcatPlayables = ModManager.ActiveMods.Any(x => x.id == "sprobgik.desecratinggraves");
            if (NotSlugcatPlayables) ScavolutionPlugin.pubLogger?.LogDebug("not playable slugcats has been enabled!");
            M4rblelousEntityPack = ModManager.ActiveMods.Any(x => x.id == "lb-fgf-m4r-ik.modpack");
            if (M4rblelousEntityPack) ScavolutionPlugin.pubLogger?.LogDebug("The M4rblelous Entity Pack has been enabled!");
        }

        static bool ControlledScavenger(AbstractCreature scav)
        {
            return (ModManager.MSC && scav.controlled) || (NotSlugcatPlayables && NotSlugcatPlayableScavengerImpl(scav));
        }

        static bool NotSlugcatPlayableScavengerImpl(AbstractCreature scav)
        {
            if (NotSlugcatPlayables)
            {
                return SprobDesecratingGraves.ScavengerHooks.GetScavengerData(scav).controller != null;
            }
            return false;
        }
    }
}
