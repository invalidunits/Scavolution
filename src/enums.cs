namespace Scavolution
{
    static class SECreatureEnums
    {
        public static CreatureTemplate.Type? ScavengerJunior { get; private set; }
        public static CreatureTemplate.Type? ScavengerProphet { get; private set; }

        static public void RegisterEnums()
        {
            ScavengerJunior = new CreatureTemplate.Type("Scavolution_ScavengerJunior", true);

            if (ModManager.MSC && ModManager.Watcher)
            {
                ScavengerProphet = new CreatureTemplate.Type("Scavolution_ScavengerProphet", true);
            }
        }

        static public void UnregisterEnums()
        {
            if (ScavengerJunior is not null)
            {
                ScavengerJunior.Unregister();
                ScavengerJunior = null;
            }

            if (ScavengerProphet is not null)
            {
                ScavengerProphet.Unregister();
                ScavengerProphet = null;
            }
        }
    }

    static class SEMultiplayerUnlocks
    {
        public static MultiplayerUnlocks.SandboxUnlockID? ScavengerJunior { get; private set; } = null;
        public static MultiplayerUnlocks.SandboxUnlockID? ScavengerProphet { get; private set; } = null;


        static public void RegisterEnums()
        {
            ScavengerJunior = new MultiplayerUnlocks.SandboxUnlockID("Scavolution_ScavengerJunior", true);
            if (!MultiplayerUnlocks.CreatureUnlockList.Contains(ScavengerJunior))
            {
                MultiplayerUnlocks.CreatureUnlockList.Insert(MultiplayerUnlocks.CreatureUnlockList.IndexOf(MultiplayerUnlocks.SandboxUnlockID.Scavenger), ScavengerJunior);
            }

            if (ModManager.MSC && ModManager.Watcher)
            {
                ScavengerProphet = new MultiplayerUnlocks.SandboxUnlockID("Scavolution_ScavengerProphet", true);
                if (!MultiplayerUnlocks.CreatureUnlockList.Contains(ScavengerProphet))
                {
                    if (MultiplayerUnlocks.CreatureUnlockList.Contains(MoreSlugcats.MoreSlugcatsEnums.SandboxUnlockID.ScavengerElite))
                    {
                        MultiplayerUnlocks.CreatureUnlockList.Insert(MultiplayerUnlocks.CreatureUnlockList.IndexOf(MoreSlugcats.MoreSlugcatsEnums.SandboxUnlockID.ScavengerElite), ScavengerProphet);
                    }
                    else
                    {
                        MultiplayerUnlocks.CreatureUnlockList.Insert(MultiplayerUnlocks.CreatureUnlockList.IndexOf(MultiplayerUnlocks.SandboxUnlockID.Scavenger), ScavengerProphet);
                    }
                }
            }
        }
        
        static public void UnregisterEnums()
        {
            if (ScavengerJunior is not null)
            {
                ScavengerJunior.Unregister();
                ScavengerJunior = null;
            }

            if (ScavengerProphet is not null)
            {
                ScavengerProphet.Unregister();
                ScavengerProphet = null;
            }
        }
    }


    static class SESocialEvent
    {
        public static SocialEventRecognizer.EventID? JuniorNuisance { get; private set; }

        static public void RegisterEnums()
        {
            JuniorNuisance = new SocialEventRecognizer.EventID("Scavolution_JuniorNuisance", true);
        }

        static public void UnregisterEnums()
        {
            if (JuniorNuisance is not null)
            {
                JuniorNuisance.Unregister();
                JuniorNuisance = null;
            }
        }
    }

    static class SEScavengerBehaviors
    {
        public static ScavengerAI.Behavior? FollowParent { get; private set; }

        static public void RegisterEnums()
        {
            FollowParent = new ScavengerAI.Behavior("Scavolution_FollowParent", true);
        }

        static public void UnregisterEnums()
        {
            if (FollowParent is not null)
            {
                FollowParent.Unregister();
                FollowParent = null;
            }
        }
    }
}
