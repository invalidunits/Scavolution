using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;

namespace Scavolution 
{
    public partial class ScavolutionPlugin
    {
        static bool NotSlugcatPlayables = false;
        static void InitializeModCompatibility()
        {
            NotSlugcatPlayables = ModManager.ActiveMods.Any(x => x.id == "sprobgik.desecratinggraves");
            if (NotSlugcatPlayables) ScavolutionPlugin.pubLogger?.LogDebug("not playable slugcats has been enabled!");
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
