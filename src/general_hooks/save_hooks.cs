using System;
using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Scavolution
{
    public partial class ScavolutionPlugin
    {
        public void SaveHooks()
        {
            On.RegionState.CreatureToStringInDenPos += RegionState_CreatureToStringInDenPos;
            IL.WorldLoader.GeneratePopulation += WorldLoader_GeneratePopulation;

            juniorSpawnChance.Clear();
            juniorSpawnChance.Add(SlugcatStats.Timeline.White, 0.2f);
            juniorSpawnChance.Add(SlugcatStats.Timeline.Yellow, 0.3f);
            juniorSpawnChance.Add(SlugcatStats.Timeline.Red, 0.05f);

            if (ModManager.MSC)
            {
                juniorSpawnChance.Add(SlugcatStats.Timeline.Gourmand, 0.3f);
                juniorSpawnChance.Add(SlugcatStats.Timeline.Artificer, 0.01f);
                juniorSpawnChance.Add(SlugcatStats.Timeline.Rivulet, 0.1f);
                juniorSpawnChance.Add(SlugcatStats.Timeline.Spear, 0.01f);
                juniorSpawnChance.Add(SlugcatStats.Timeline.Saint, 0.3f);
            }

            if (ModManager.Watcher)
            {
                juniorSpawnChance.Add(SlugcatStats.Timeline.Watcher, 0.3f);
            }

        }

        public static Dictionary<SlugcatStats.Timeline, float> juniorSpawnChance = new();

        public void WorldLoader_GeneratePopulation(ILContext context)
        {
            try
            {
                int i = 0;
                ILCursor cursor = new(context);
                while (cursor.TryGotoNext(MoveType.After, x => x.MatchNewobj<AbstractCreature>()))
                {
                    i++;
                    cursor.Emit(OpCodes.Dup);
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.Emit(OpCodes.Ldarg_1);
                    cursor.Emit(OpCodes.Ldloc, 5);
                    cursor.EmitDelegate((AbstractCreature spawnedCreature, WorldLoader loader, bool fresh, int spawnerindex) =>
                    {
                        if (!loader.game.IsStorySession) return;
                        if (spawnedCreature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Scavenger &&
                            spawnedCreature.creatureTemplate.type != SECreatureEnums.ScavengerJunior &&
                            fresh && options.JuniorsSpawnNaturally.Value)
                        {
                            UnityEngine.Random.State state = UnityEngine.Random.state;
                            try
                            {
                                UnityEngine.Random.InitState(spawnedCreature.ID.RandomSeed);

                                float spawnChance;
                                if (!juniorSpawnChance.TryGetValue(loader.game.TimelinePoint, out spawnChance)) spawnChance = 0.1f;

                                if (UnityEngine.Random.value < spawnChance)
                                {
                                    var junior = new AbstractCreature(loader.world, StaticWorld.GetCreatureTemplate(SECreatureEnums.ScavengerJunior), null, loader.spawners[spawnerindex].den, loader.game.GetNewID());
                                    AbstractRoom abstractRoom = loader.world.GetAbstractRoom(loader.spawners[spawnerindex].den);
                                    abstractRoom.MoveEntityToDen(junior);
                                    
                                    JuniorState juniorstate = JuniorState.map.GetValue(junior.state, (x) => throw new Exception("no junior state?"));
                                    juniorstate.currentParent = spawnedCreature.ID.number;
                                }
                            }
                            catch (Exception except)
                            {
                                Logger.LogError(except);
                            }
                            finally
                            {
                                UnityEngine.Random.state = state;
                            }
                        }
                    });
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        
        public string RegionState_CreatureToStringInDenPos(On.RegionState.orig_CreatureToStringInDenPos orig, RegionState self, AbstractCreature critter, int validSaveShelter, int activeGate)
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

                UnityEngine.Random.State state = UnityEngine.Random.state;
                UnityEngine.Random.InitState(critter.ID.RandomSeed + (int)System.DateTime.Now.Ticks);
                float randomEvolutionChance = options.ScavengerEvolutionRandomChance.Value / 100f;
                if (!evolution_tracker.successfulUpgrade.HasValue && UnityEngine.Random.value < randomEvolutionChance)
                {
                    if (template.type.GetRandomEvolution(out var randomevolution))
                    {
                        evolution_tracker.successfulHelpers.Clear();
                        evolution_tracker.successfulUpgrade = randomevolution;
                    }
                }
                UnityEngine.Random.state = state;

                if (evolution_tracker.successfulUpgrade.HasValue && canEvolve)
                {
                    critter.creatureTemplate = StaticWorld.GetCreatureTemplate(evolution_tracker.successfulUpgrade.Value.ends_as);
                    if (options.ScavengersAppreciateEvolving.Value)
                    {
                        foreach (var helper in evolution_tracker.successfulHelpers)
                        {
                            var memoryofhelper = critter.state.socialMemory.GetOrInitiateRelationship(helper);
                            memoryofhelper.InfluenceLike(0.8f);
                            memoryofhelper.InfluenceFear(-0.8f);
                            memoryofhelper.InfluenceKnow(1.0f);
                        }
                    }



                    string upgrade = orig(self, critter, validSaveShelter, activeGate);
                    critter.creatureTemplate = template;

                    if (options.JuniorsSpawnWhenEvolving.Value)
                    {
                        for (int i = 0; i < evolution_tracker.successfulUpgrade.Value.max_juniors_spawned; i++)
                        {
                            var junior = new AbstractCreature(self.world, StaticWorld.GetCreatureTemplate(SECreatureEnums.ScavengerJunior), null, worldCoordinate, self.world.game.GetNewID());
                            junior.abstractAI.denPosition = worldCoordinate;
                            junior.spawnDen = worldCoordinate;
                            junior.pos = worldCoordinate;
                            ScavolutionPlugin.pubLogger?.LogDebug($"Spawning Scavenger Junior! {junior} with parent {critter}");
                            JuniorState juniorState = JuniorState.map.GetValue(junior.state, (x) => throw new Exception("no junior state?"));
                            juniorState.currentParent = critter.ID.number;
                            self.savedPopulation.Add(self.CreatureToStringInDenPos(junior, validSaveShelter, activeGate));
                        }
                    }

                    return upgrade;
                }
            }


            return orig(self, critter, validSaveShelter, activeGate);
        }
    }
}