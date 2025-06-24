


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace Scavolution
{
    static class AbstractEvolutionTrackerExtension
    {
        static public AbstractEvolutionTracker GetEvolutionTracker(this ScavengerAbstractAI aI)
        {
            AbstractEvolutionTracker tracker;
            if (!AbstractEvolutionTracker.map.TryGetValue(aI, out tracker))
            {
                tracker = new AbstractEvolutionTracker(aI);
            }
            return tracker;
        }
    }
    class AbstractEvolutionTracker
    {
        public readonly static ConditionalWeakTable<ScavengerAbstractAI, AbstractEvolutionTracker> map = new ConditionalWeakTable<ScavengerAbstractAI, AbstractEvolutionTracker>();
        public Dictionary<WeakReference<AbstractPhysicalObject>, EntityID> evolutionHelpers = new();
        public List<EntityID> successfulHelpers = new();
        readonly WeakReference<ScavengerAbstractAI> AI;
        public bool upgradeOpertunity { get; private set; } = false;
        public EvolutionRecipe? successfulUpgrade { get; set; } = null;

        public AbstractEvolutionTracker(ScavengerAbstractAI aI)
        {
            AbstractEvolutionTracker.map.Add(aI, this);
            AI = new WeakReference<ScavengerAbstractAI>(aI);
        }

        public void CheckSuccess(bool inDen)
        {
            UnityEngine.Debug.Log($"Cheking Upgrade {inDen}");
            if (!AI.TryGetTarget(out var scavAI)) return;
            for (int i = scavAI.parent.stuckObjects.Count - 1; i >= 0; i--)
            {
                if (scavAI.parent.stuckObjects[i] is AbstractPhysicalObject.CreatureGripStick && scavAI.parent.stuckObjects[i].A == scavAI.parent)
                {
                    if (EvolutionTree.TryGetEvolution(scavAI.parent.creatureTemplate.type, scavAI.parent.stuckObjects[i].B.type, out var evolution))
                    {
                        foreach (var helper in evolutionHelpers.Where(x =>
                            {
                                x.Key.TryGetTarget(out var target);
                                return target == scavAI.parent.stuckObjects[i].B;
                            }))
                        {
                            if (!successfulHelpers.Contains(helper.Value)) successfulHelpers.Add(helper.Value);
                        }

                        if (inDen)
                        {
                            scavAI.DropAndDestroy(scavAI.parent.stuckObjects[i]);
                        }
                        successfulUpgrade = evolution!;
                        UnityEngine.Debug.Log($"{scavAI.parent.type} is upgrading into {successfulUpgrade.Value.ends_as}");
                    }
                }
            }
            evolutionHelpers.Clear(); 

        }

        public void Update()
        {
            

            upgradeOpertunity = false;
            if (!AI.TryGetTarget(out var scavAI)) return;
            if (scavAI.world.game.session is StoryGameSession)
            {
                foreach (AbstractPhysicalObject.CreatureGripStick stick in scavAI.parent.stuckObjects.OfType<AbstractPhysicalObject.CreatureGripStick>())
                {
                    if (stick.B == scavAI.parent) continue;
                    if (!scavAI.parent.TryGetEvolution(stick.B.type, out _)) continue;
                    upgradeOpertunity = true;
                }
            }

        }
    }

    class EvolutionTracker : AIModule
    {
        AbstractCreature creature => this.AI.creature;
        ScavengerAI scavengerAI => (ScavengerAI)this.AI;
        ScavengerAbstractAI abstractAI => (ScavengerAbstractAI)creature.abstractAI;

        public EvolutionTracker(ArtificialIntelligence AI) : base(AI) { }

        public override float Utility()
        {
            return abstractAI.GetEvolutionTracker().upgradeOpertunity ? 1.0f : 0.0f;
        }
    }
    


}