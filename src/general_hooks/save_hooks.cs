namespace Scavolution
{
    public partial class ScavolutionPlugin
    {
        public void SaveHooks()
        {
            On.RegionState.CreatureToStringInDenPos += RegionState_CreatureToStringInDenPos;
        }


        bool upgrade_scavengers = true;
        public void RegionState_AdaptWorldToRegionState()
        {

        }
        public string RegionState_CreatureToStringInDenPos(On.RegionState.orig_CreatureToStringInDenPos orig, RegionState self, AbstractCreature critter, int validSaveShelter, int activeGate)
        {
            if (upgrade_scavengers)
            {
                CreatureTemplate template = critter.creatureTemplate;
                if (critter.abstractAI is ScavengerAbstractAI scavAI)
                {
                    var evolution_tracker = scavAI.GetEvolutionTracker();
                    evolution_tracker.CheckSuccess(false);

                    WorldCoordinate worldCoordinate = critter.spawnDen;
                    if (self.world.GetAbstractRoom(critter.pos).shelter && ShelterDoor.IsTileInsideShelterRange(self.world.GetAbstractRoom(critter.pos), critter.pos.Tile) && (critter.pos.room == validSaveShelter || critter.state.dead || critter.creatureTemplate.offScreenSpeed == 0f))
                    {
                        worldCoordinate = new WorldCoordinate(critter.pos.room, -1, -1, 0);
                    }
                    else if (critter.abstractAI != null && critter.abstractAI.denPosition.HasValue && self.world.IsRoomInRegion(critter.abstractAI.denPosition.Value.room))
                    {
                        worldCoordinate = critter.abstractAI.denPosition.Value;
                    }

                    bool canEvolve = true;
                    if (self.world.GetAbstractRoom(worldCoordinate).shelter || !critter.state.alive)
                    {
                        canEvolve = false;
                    }
                    

                    if (evolution_tracker.successfulUpgrade.HasValue && canEvolve)
                    {
                        critter.creatureTemplate = StaticWorld.GetCreatureTemplate(evolution_tracker.successfulUpgrade.Value.ends_as);
                        foreach (var helper in evolution_tracker.successfulHelpers)
                        {
                            var memoryofhelper = critter.state.socialMemory.GetOrInitiateRelationship(helper);
                            memoryofhelper.InfluenceLike(0.8f);
                            memoryofhelper.InfluenceFear(-0.8f);
                            memoryofhelper.InfluenceKnow(1.0f);
                        }



                        string upgrade = orig(self, critter, validSaveShelter, activeGate);
                        critter.creatureTemplate = template;
                        var random = new System.Random();
                        for (int i = 0; i < evolution_tracker.successfulUpgrade.Value.max_juniors_spawned; i++)
                        {
                            if (random.Next() % 3 < 2)
                            {
                                ScavolutionPlugin.pubLogger?.LogDebug("Spawning Scavenger Junior!");

                                var junior = new AbstractCreature(self.world, StaticWorld.GetCreatureTemplate(SECreatureEnums.ScavengerJunior), null, worldCoordinate, self.world.game.GetNewID());
                                junior.abstractAI.denPosition = worldCoordinate;
                                junior.spawnDen = worldCoordinate;
                                junior.pos = worldCoordinate;
                                JuniorState state = (JuniorState)junior.state;
                                state.currentParent = critter.ID.number;
                                self.savedPopulation.Add(self.CreatureToStringInDenPos(junior, validSaveShelter, activeGate));
                            }
                        }




                        return upgrade;
                    }
                }
                
            }

            return orig(self, critter, validSaveShelter, activeGate);
        }
        
        

    }
}