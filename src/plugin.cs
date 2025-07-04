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

        public ScavolutionOptionsMenu options = new();
        void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            plugin = this;
            orig(self);
            
            try
            {
                On.Menu.MainMenu.ctor += MainMenu_ctor;

                MachineConnector.SetRegisteredOI("invalidunits.scavolution", options);
                EvolutionTree.InitializeEvolutions();
                Logger.LogDebug("Finished Evolution Init");

                SECreatureEnums.RegisterEnums();
                SESocialEvent.RegisterEnums();
                SEScavengerBehaviors.RegisterEnums();
                SEMultiplayerUnlocks.RegisterEnums();
                Logger.LogDebug("Finished registering Enums.");


                ScavengerAIHooks();
                Logger.LogDebug("Finished Hooking AI.");

                RegisterScavengerJunior();
                Logger.LogDebug("Finished registering scav junior");

                SaveHooks();
                Logger.LogDebug("Finished Hooking Saving.");    

            }
            catch (Exception except)
            {
                failedInitialization = true;
                UnityEngine.Debug.Log(except);
            }

            
        
        }

        bool failedInitialization = false, showedFailedInitialization = false;
        bool showedNoDLCWarning = false;
        void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, Menu.MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            if (!(ModManager.DLCShared || ModManager.Watcher) && !showedNoDLCWarning)
            {
                showedNoDLCWarning = true;
                manager.ShowDialog(new Menu.DialogNotify(
                    "Scavolution: No DLC has been enabled. Scavengers won't evolve.",
                    manager, () => { }));
            }
            else if (failedInitialization && !showedFailedInitialization)
            {
                showedFailedInitialization = true;
                manager.ShowDialog(new Menu.DialogNotify(
                    "Scavolution: \n Scavolution has failed to start up. Please restart your game. \n If this message continues to show, please disable the mod before playing.",
                    manager, () => { }));
            }

            orig(self, manager, showRegionSpecificBkg);
            
        }



    }
}
