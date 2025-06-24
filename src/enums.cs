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
