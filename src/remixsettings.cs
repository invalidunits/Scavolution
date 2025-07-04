using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace Scavolution 
{
    public class ScavolutionOptionsMenu : OptionInterface
    {

        public Configurable<bool> JuniorsSpawnNaturally;
        public Configurable<bool> JuniorsSpawnWhenEvolving;

        public Configurable<bool> ScavengersAppreciateEvolving;
        public Configurable<int> ScavengerEvolutionRandomChance;

        static string[] splashText = {
            "One of Scavs's movies are (still) peak fiction.",
            "It's \"The Scavolution\". Coming to iterators near you.",
            "When life gives you children, give them boomerangs and grenades. (please dont give them grenades)",
            "Juniors may be included.",
            "Scavengers work a 9 to 5. When will you start picking up slack",
            "Artificer fans NOT welcome. (joking <3)",
            "You're getting a promotion!",
            "Boomerangs solve most problems.",
            "Don't forget to tip your Monkeys.",
            "Evolution is just another word for improvement.",
            "Scavengers are the true underdogs.",
            "The More Scavenger's Expansion",
        };

        public IEnumerable<string> possibleSplashTexts() {
            return splashText.AsEnumerable();
        }

        public void randomizeSplashText() {
            string[]? splash_text = possibleSplashTexts().ToArray();
            System.Random rand = new System.Random((int)System.DateTime.Now.Ticks);
            if (header != null) {
                    header.description = splash_text[rand.Next() % splash_text.Length];
            }   
        }

        OpLabel? header = null;

        public ScavolutionOptionsMenu() : base()
        {
            JuniorsSpawnNaturally = this.config.Bind<bool>("JuniorsSpawnNaturally", true);
            JuniorsSpawnNaturally.info.description = "Juniors spawn naturally alongside scavengers (They don't respawn, so take care of them.)";
            JuniorsSpawnWhenEvolving = this.config.Bind<bool>("JuniorsSpawnWhenEvolving", true);
            JuniorsSpawnWhenEvolving.info.description = "Juniors will spawn with Scavengers when they evolve.";

            ScavengersAppreciateEvolving = this.config.Bind<bool>("ScavengersAppreciateEvolving", true);
            ScavengersAppreciateEvolving.info.description = "Scavengers will personally like you if you help them evolve. (This doesn't effect reputation changes, only behavior)\n(Without this option, Evolving a scavenger into an elite will result in them killing you.)";

            ScavengerEvolutionRandomChance = this.config.Bind<int>("ScavengerEvolutionRandomChance", 0);
            ScavengerEvolutionRandomChance.info.description = "The chance of scavengers evolving without any player intervension (0%-100%. Disabled by default)";
        }

        public override void Initialize()
        {
            base.Initialize();
        
            var GeneralTab = new OpTab(this, "General");
            header = new OpLabel(new Vector2(300f - 100f, 550f), new Vector2(200f, 40f), "Scavolution", bigText: true) { description = "", };
            header.OnUnload += randomizeSplashText;
            randomizeSplashText();

            GeneralTab.AddItems(
                header,
                new OpLabel(0f, 450f, "Scavengers appreciate evolving:"), new OpCheckBox(ScavengersAppreciateEvolving, 200f, 450f) { description = ScavengersAppreciateEvolving?.info?.description },
                new OpLabel(0f, 400f, "Scavengers randomly evolving chance:"), new OpSlider(ScavengerEvolutionRandomChance, new Vector2(230f, 400f), 1.3f) { description = ScavengerEvolutionRandomChance?.info?.description },
                new OpLabel(0f, 350f, "Juniors naturally spawn:"), new OpCheckBox(JuniorsSpawnNaturally, 200f, 350f) { description = JuniorsSpawnNaturally?.info?.description },
                new OpLabel(0f, 300f, "Juniors spawn during evolution: "), new OpCheckBox(JuniorsSpawnWhenEvolving, 200f, 300f) { description = JuniorsSpawnWhenEvolving?.info?.description }
            ); 
            
            this.Tabs = new OpTab[] {
                GeneralTab
            };
        }
    }
}
