namespace Scavolution
{
    static class SECreatureEnums
    {
        public static CreatureTemplate.Type? ScavengerJunior { get; private set; }

        static public void RegisterEnums()
        {
            ScavengerJunior = new CreatureTemplate.Type("Scavolution_ScavengerJunior", true);
        }
    }
    
    static class SEMultiplayerUnlocks
    {
        public static MultiplayerUnlocks.SandboxUnlockID? ScavengerJunior { get; private set; }

        static public void RegisterEnums()
        {
            ScavengerJunior = new MultiplayerUnlocks.SandboxUnlockID("Scavolution_ScavengerJunior", true);
            if (!MultiplayerUnlocks.CreatureUnlockList.Contains(ScavengerJunior))
			{
				MultiplayerUnlocks.CreatureUnlockList.Insert(MultiplayerUnlocks.CreatureUnlockList.IndexOf(MultiplayerUnlocks.SandboxUnlockID.Scavenger), ScavengerJunior);
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

    }

    static class SEScavengerBehaviors
    {
        public static ScavengerAI.Behavior? FollowParent { get; private set; }

        static public void RegisterEnums()
        {
            FollowParent = new ScavengerAI.Behavior("Scavolution_FollowParent", true);
        }
    }
}
