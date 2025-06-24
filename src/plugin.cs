using System;
using BepInEx;
using BepInEx.Logging;

namespace Scavolution 
{

    [BepInPlugin("invalidunits.scavolution", "Scavolution", "0.1")]
    public partial class ScavolutionPlugin : BaseUnityPlugin
    {
        public static ManualLogSource? pubLogger => plugin?.Logger;
        public static ScavolutionPlugin? plugin;
        private void Awake()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }


        void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            plugin = this;
            orig(self);
            
            try
            {

                Logger.LogDebug("Initializing Scavolution");
                EvolutionTree.InitializeEvolutions();
                Logger.LogDebug("Finished Evolution Init");
                ScavengerAIHooks();
                SaveHooks();

                Logger.LogDebug("Finished Hooking AI And Save.");
                SECreatureEnums.RegisterEnums();
                SESocialEvent.RegisterEnums();
                SEScavengerBehaviors.RegisterEnums();
                Logger.LogDebug("Finished RegisterEnums.");
                RegisterScavengerJunior();

                On.Menu.MainMenu.ctor += MainMenu_ctor;

            }
            catch (Exception except)
            {
                failedInitialization = true;
                Logger.LogError(except);
            }

            
        
        }

        bool failedInitialization = false, showedFailedInitialization = false;
        bool showedNoDLCWarning = false;
        void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, Menu.MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            if (!(ModManager.DLCShared || ModManager.Watcher) && !showedNoDLCWarning)
            {
                showedNoDLCWarning = true;
                manager.ShowDialog(new Menu.DialogNotify("Scavolution: No DLC has been enabled. Scavengers won't evolve.", manager, () => { }));
            }

            if (failedInitialization && !showedFailedInitialization)
            {
                showedFailedInitialization = true;
                manager.ShowDialog(new Menu.DialogNotify("Scavolution: \n Scavolution has failed to start up. Please restart your game. \n If this message continues to show disable the mod before continuing.", manager, () => { }));
            }

            orig(self, manager, showRegionSpecificBkg);
            
        }



    }
}
