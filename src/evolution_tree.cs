
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scavolution 
{
    public struct EvolutionRecipe {
        public CreatureTemplate.Type starts_as;
        public CreatureTemplate.Type ends_as;
        public AbstractPhysicalObject.AbstractObjectType item_required;
        public uint max_juniors_spawned;

        public EvolutionRecipe(CreatureTemplate.Type starts_as, CreatureTemplate.Type ends_as,
            AbstractPhysicalObject.AbstractObjectType item_required, uint max_juniors_spawned = 0)
        {
            this.starts_as = starts_as;
            this.ends_as = ends_as;
            this.item_required = item_required;
            this.max_juniors_spawned = max_juniors_spawned;
        }
    }

    public static class EvolutionTree
    {
        static public EvolutionRecipe[]? recipes = null;

        static public bool TryGetEvolution(this CreatureTemplate.Type type, AbstractPhysicalObject.AbstractObjectType item, out EvolutionRecipe? evolution)
        {
            evolution = null;
            if (recipes != null)
            {
                for (int i = 0; i < recipes.Count(); i++)
                {
                    ref EvolutionRecipe recipe = ref recipes[i];
                    if (recipe.starts_as.index != type.index) continue;
                    if (recipe.ends_as.index == type.index) continue;
                    if (recipe.item_required.index != item.index) continue;
                    evolution = recipe;
                    return true;
                }
            }
            return false;
        }


        static public bool GetRandomEvolution(this CreatureTemplate.Type type, out EvolutionRecipe? evolution)
        {
            evolution = null;
            if (recipes != null)
            {
                List<EvolutionRecipe> valid_recipes = new();

                for (int i = 0; i < recipes.Count(); i++)
                {
                    ref EvolutionRecipe recipe = ref recipes[i];
                    if (recipe.starts_as.index == type.index) continue;
                    if (recipe.ends_as.index != type.index) continue;
                    valid_recipes.Add(recipe);
                    return true;
                }

                UnityEngine.Random.State state = UnityEngine.Random.state;
                UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);


                evolution = valid_recipes[(int)Mathf.Floor((float)valid_recipes.Count * UnityEngine.Random.value)];
                UnityEngine.Random.state = state;
                return true;
            }
            return false;
        }

        static public bool TryGetEvolution(this AbstractCreature creature, AbstractPhysicalObject.AbstractObjectType item, out EvolutionRecipe? evolution) => TryGetEvolution(creature.creatureTemplate.type, item, out evolution);


        static public void InitializeEvolutions()
        {
            List<EvolutionRecipe> list_recipes = new List<EvolutionRecipe>();

            if (ModManager.DLCShared)
            {
                list_recipes.Add(new EvolutionRecipe(CreatureTemplate.Type.Scavenger,
                    DLCSharedEnums.CreatureTemplateType.ScavengerElite, AbstractPhysicalObject.AbstractObjectType.VultureMask, 1));
                list_recipes.Add(new EvolutionRecipe(CreatureTemplate.Type.Scavenger,
                    DLCSharedEnums.CreatureTemplateType.ScavengerElite, DLCSharedEnums.AbstractObjectType.SingularityBomb, 1));
            }

            if (ModManager.Watcher)
            {
                list_recipes.Add(new EvolutionRecipe(CreatureTemplate.Type.Scavenger,
                    Watcher.WatcherEnums.CreatureTemplateType.ScavengerTemplar, Watcher.WatcherEnums.AbstractObjectType.Boomerang, 1));
                list_recipes.Add(new EvolutionRecipe(CreatureTemplate.Type.Scavenger,
                    Watcher.WatcherEnums.CreatureTemplateType.ScavengerTemplar, AbstractPhysicalObject.AbstractObjectType.KarmaFlower, 1));
                list_recipes.Add(new EvolutionRecipe(Watcher.WatcherEnums.CreatureTemplateType.ScavengerTemplar,
                    Watcher.WatcherEnums.CreatureTemplateType.ScavengerDisciple, AbstractPhysicalObject.AbstractObjectType.KarmaFlower, 1));


                if (ModManager.DLCShared)
                {
                    list_recipes.Add(new EvolutionRecipe(DLCSharedEnums.CreatureTemplateType.ScavengerElite,
                        Watcher.WatcherEnums.CreatureTemplateType.ScavengerDisciple, AbstractPhysicalObject.AbstractObjectType.KarmaFlower, 1));
                    list_recipes.Add(new EvolutionRecipe(DLCSharedEnums.CreatureTemplateType.ScavengerElite,
                        Watcher.WatcherEnums.CreatureTemplateType.ScavengerDisciple, Watcher.WatcherEnums.AbstractObjectType.Boomerang, 1));
                }
            }

            if (ScavolutionPlugin.M4rblelousEntityPack)
            {
                AddM4rblelousEntityPackRecipes(ref list_recipes);
            }
            
            recipes = list_recipes.ToArray();
        }


        static void AddM4rblelousEntityPackRecipes(ref List<EvolutionRecipe> list_recipes)
        {
            list_recipes.Add(new EvolutionRecipe(DLCSharedEnums.CreatureTemplateType.ScavengerElite,
                        LBMergedMods.Enums.CreatureTemplateType.ScavengerSentinel, AbstractPhysicalObject.AbstractObjectType.VultureMask, 1));
            list_recipes.Add(new EvolutionRecipe(DLCSharedEnums.CreatureTemplateType.ScavengerElite,
                    LBMergedMods.Enums.CreatureTemplateType.ScavengerSentinel, LBMergedMods.Enums.AbstractObjectType.ThornyStrawberry, 1));
        }
    }
}