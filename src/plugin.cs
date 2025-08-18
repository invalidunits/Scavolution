using System;
using BepInEx;
using BepInEx.Logging;

namespace Scavolution 
{

    // [BepInDependency("sprobgik.desecratinggraves", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("invalidunits.scavolution", "Scavolution", "1.1")]
    public partial class ScavolutionPlugin : BaseUnityPlugin
    {
        public static ManualLogSource? pubLogger => plugin?.Logger;
        public static bool Init = false;
        public static ScavolutionPlugin? plugin;
        private void Awake()
        {
            On.RainWorld.PostModsInit += RainWorld_PostModsInit;
        }

        public ScavolutionOptionsMenu options = new();
        void RainWorld_PostModsInit(On.RainWorld.orig_PostModsInit orig, RainWorld self)
        {
            plugin = this;
            orig(self);
            
            try
            {
                if (!Init)
                {
                    Init = true;
                    InitializeModCompatibility();
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
            if (failedInitialization && !showedFailedInitialization)
            {
                showedFailedInitialization = true;
                manager.ShowDialog(new Menu.DialogNotify(
                    "Scavolution: \n Scavolution has failed to start up. Please restart your game. \n If this message continues to show, please disable the mod before playing.",
                    manager, () => { }));
            }
            else if (!(ModManager.DLCShared || ModManager.Watcher) && !showedNoDLCWarning)
            {
                showedNoDLCWarning = true;
                manager.ShowDialog(new Menu.DialogNotify(
                    "Scavolution: No DLC has been enabled. Scavengers won't evolve.",
                    manager, () => { }));
            }

            orig(self, manager, showRegionSpecificBkg);
        }



    }
}
