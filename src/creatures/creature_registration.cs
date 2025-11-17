using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RWCustom;
using UnityEngine;

namespace Scavolution
{
    partial class ScavolutionPlugin
    {
        void RegisterCreatures()
        {
            On.StaticWorld.InitCustomTemplates += StaticWorld_InitCustomTemplates;
            On.StaticWorld.InitStaticWorldRelationships += StaticWorld_InitStaticWorldRelationships;
            On.AbstractCreature.ctor += AbstractCreature_ctor;


            RegisterScavengerJunior();
            RegisterScavengerImperial();

            // Arena stuff
            if (!Futile.atlasManager.DoesContainAtlas("atlases/Kill_ScavengerJunior"))
            {
                Futile.atlasManager.LoadImage("atlases/Kill_ScavengerJunior");
            }
            On.CreatureSymbol.SpriteNameOfCreature += ScavengerJunior_CreatureSymbol_SpriteNameOfCreature;
            On.CreatureSymbol.ColorOfCreature += ScavengerJunior_CreatureSymbol_ColorOfCreature;
            On.MultiplayerUnlocks.SandboxItemUnlocked += ScavengerJunior_MultiplayerUnlocks_SandboxItemUnlocked;
        }


        void StaticWorld_InitStaticWorldRelationships(On.StaticWorld.orig_InitStaticWorldRelationships orig)
        {
            orig();
            try
            {
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerJunior, CreatureTemplate.Type.Overseer, new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Uncomfortable, 0.5f));


                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.Overseer,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Uncomfortable, 0.2f));
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.LizardTemplate,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 0.7f));
                // StaticWorld.EstablishRelationship(CreatureTemplate.Type.LizardTemplate, SECreatureEnums.ScavengerImperial,
                // new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 0.7f));
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.Vulture,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 0.7f));
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.KingVulture,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1.0f));
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.RedLizard,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 0.5f));
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.RedCentipede,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1.0f));
                
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.DaddyLongLegs,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1.0f));
                StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, CreatureTemplate.Type.BrotherLongLegs,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1.0f));

                if (ModManager.DLCShared)
                {
                    StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, DLCSharedEnums.CreatureTemplateType.MirosVulture,
                        new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1.0f));
                }
                
                if (ModManager.MSC)
                {
                    StaticWorld.EstablishRelationship(SECreatureEnums.ScavengerImperial, MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard,
                        new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Attacks, 1.0f));
                }
                
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }

        public string ScavengerJunior_CreatureSymbol_SpriteNameOfCreature(On.CreatureSymbol.orig_SpriteNameOfCreature orig, IconSymbol.IconSymbolData iconData)
        {
            if (iconData.critType == SECreatureEnums.ScavengerJunior)
            {
                return "atlases/Kill_ScavengerJunior";
            }

            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (iconData.critType == SECreatureEnums.ScavengerImperial)
                {
                    return "Kill_ScavengerElite";
                }
            }

            return orig(iconData);
        }

        public Color ScavengerJunior_CreatureSymbol_ColorOfCreature(On.CreatureSymbol.orig_ColorOfCreature orig, IconSymbol.IconSymbolData iconData)
        {
            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (iconData.critType == SECreatureEnums.ScavengerImperial)
                {
                    return new Color(46f / 51f, 0.05490196f, 0.05490196f);
                }
            }

            return orig(iconData);
        }


        bool ScavengerJunior_MultiplayerUnlocks_SandboxItemUnlocked(On.MultiplayerUnlocks.orig_SandboxItemUnlocked orig, MultiplayerUnlocks self, MultiplayerUnlocks.SandboxUnlockID unlockID)
        {
            if (unlockID == SEMultiplayerUnlocks.ScavengerJunior)
            {
                return self.SandboxItemUnlocked(MultiplayerUnlocks.SandboxUnlockID.Scavenger);
            }

            if (SECreatureEnums.ScavengerImperial != null)
            {
                if (unlockID == SEMultiplayerUnlocks.ScavengerImperial)
                {
                    return self.SandboxItemUnlocked(MultiplayerUnlocks.SandboxUnlockID.Scavenger);
                }
            }

            return orig(self, unlockID);
        }


        void AbstractCreature_ctor(On.AbstractCreature.orig_ctor orig, AbstractCreature self, World world, CreatureTemplate creatureTemplate, Creature realizedCreature, WorldCoordinate pos, EntityID ID)
        {
            orig(self, world, creatureTemplate, realizedCreature, pos, ID);
            try
            {
                if (creatureTemplate.type == SECreatureEnums.ScavengerJunior)
                {
                    self.abstractAI = new ScavengerAbstractAI(self.world, self);
                    self.state = new HealthState(self);
                }

                if (SECreatureEnums.ScavengerImperial != null)
                {
                    if (creatureTemplate.type == SECreatureEnums.ScavengerImperial)
                    {
                        self.abstractAI = new ScavengerAbstractAI(self.world, self);
                        self.state = new HealthState(self);

                        self.personality.aggression = Mathf.Lerp(self.personality.aggression, 1.0f, 0.5f);
                        self.personality.dominance = Mathf.Lerp(self.personality.dominance, 1.0f, 0.8f);
                        self.personality.nervous = Mathf.Lerp(self.personality.nervous, 0f, 0.5f);
                        // self.personality.sympathy = Mathf.Lerp(self.personality.sympathy, 1.0f, 0.8f);
                        self.personality.energy = Mathf.Max(0.7f, self.personality.energy);
                        self.personality.bravery = 1.0f;
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        
        CreatureTemplate? ScavengerJuniorTemplate = null;
        CreatureTemplate? ScavengerImperialTemplate = null;

        public void StaticWorld_InitCustomTemplates(On.StaticWorld.orig_InitCustomTemplates orig)
        {

            orig();

            Logger.LogDebug("Initializing Scavenger Junior");
            List<TileTypeResistance> junior_tile_resistance = new List<TileTypeResistance>();
            List<TileConnectionResistance> junior_tile_connection_resistance = new List<TileConnectionResistance>();
            ScavengerJuniorTemplate = new CreatureTemplate(SECreatureEnums.ScavengerJunior,
                StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger),
                junior_tile_resistance,
                junior_tile_connection_resistance,
                new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Uncomfortable, 0.4f)
            );
            ScavengerJuniorTemplate.BlizzardWanderer = false;
            ScavengerJuniorTemplate.BlizzardAdapted = false;
            ScavengerJuniorTemplate.baseDamageResistance = 1.5f;
            ScavengerJuniorTemplate.baseStunResistance = 0.8f;
            ScavengerJuniorTemplate.instantDeathDamageLimit = 1.0f;

            ScavengerJuniorTemplate.offScreenSpeed = 1.25f;
            ScavengerJuniorTemplate.grasps = 2;
            ScavengerJuniorTemplate.AI = true;
            ScavengerJuniorTemplate.requireAImap = true;
            ScavengerJuniorTemplate.abstractedLaziness = 50;
            ScavengerJuniorTemplate.bodySize = 0.8f;
            ScavengerJuniorTemplate.doPreBakedPathing = false;
            ScavengerJuniorTemplate.preBakedPathingAncestor = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger);
            ScavengerJuniorTemplate.stowFoodInDen = false;
            ScavengerJuniorTemplate.shortcutSegments = 2;

            ScavengerJuniorTemplate.visualRadius = 1000f;
            ScavengerJuniorTemplate.movementBasedVision = 0.3f;

            ScavengerJuniorTemplate.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            ScavengerJuniorTemplate.hibernateOffScreen = true;
            ScavengerJuniorTemplate.roamBetweenRoomsChance = -1f;
            ScavengerJuniorTemplate.roamInRoomChance = -1f;
            ScavengerJuniorTemplate.socialMemory = true;
            ScavengerJuniorTemplate.communityID = CreatureCommunities.CommunityID.Scavengers;
            ScavengerJuniorTemplate.communityInfluence = 2f;
            ScavengerJuniorTemplate.dangerousToPlayer = 0.1f;

            ScavengerJuniorTemplate.meatPoints = 2;
            ScavengerJuniorTemplate.usesNPCTransportation = true;
            ScavengerJuniorTemplate.usesRegionTransportation = true;
            ScavengerJuniorTemplate.usesCreatureHoles = false;
            ScavengerJuniorTemplate.jumpAction = "Point";
            ScavengerJuniorTemplate.pickupAction = "Pick Up";
            ScavengerJuniorTemplate.throwAction = "Throw";
            ScavengerJuniorTemplate.name = "Scavenger Junior";

            for (int i = 0; i < StaticWorld.creatureTemplates.Length; i++)
            {
                if (StaticWorld.creatureTemplates[i] == null)
                {
                    StaticWorld.creatureTemplates[i] = ScavengerJuniorTemplate;
                    break;
                }
            }

            if (SECreatureEnums.ScavengerImperial != null)
            {
                Logger.LogDebug("Initializing Scavenger Imperial");
                List<TileTypeResistance> imperial_tile_resistance = new List<TileTypeResistance>();
                List<TileConnectionResistance> imperial_tile_connection_resistance = new List<TileConnectionResistance>();
                // imperial_tile_resistance.Add(new TileTypeResistance(AItile.Accessibility.Air, 1f, PathCost.Legality.Allowed));

                ScavengerImperialTemplate = new CreatureTemplate(SECreatureEnums.ScavengerImperial,
                    StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger),
                    imperial_tile_resistance,
                    imperial_tile_connection_resistance,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.1f)
                );

                ScavengerImperialTemplate.BlizzardWanderer = true;
                ScavengerImperialTemplate.BlizzardAdapted = false;
                ScavengerImperialTemplate.baseDamageResistance = 3.0f;
                ScavengerImperialTemplate.baseStunResistance = 1.5f;
                ScavengerImperialTemplate.instantDeathDamageLimit = 3.0f;

                ScavengerImperialTemplate.offScreenSpeed = 1.25f;
                ScavengerImperialTemplate.grasps = 6;
                ScavengerImperialTemplate.AI = true;
                ScavengerImperialTemplate.requireAImap = true;
                ScavengerImperialTemplate.abstractedLaziness = 50;
                ScavengerImperialTemplate.bodySize = 1.2f;
                ScavengerImperialTemplate.doPreBakedPathing = false;
                ScavengerImperialTemplate.preBakedPathingAncestor = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger);
                ScavengerImperialTemplate.stowFoodInDen = false;
                ScavengerImperialTemplate.shortcutSegments = 2;

                ScavengerImperialTemplate.visualRadius = 1000f*3f;
                ScavengerImperialTemplate.movementBasedVision = 0.3f;

                ScavengerImperialTemplate.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
                ScavengerImperialTemplate.hibernateOffScreen = true;
                ScavengerImperialTemplate.roamBetweenRoomsChance = -1f;
                ScavengerImperialTemplate.roamInRoomChance = -1f;
                ScavengerImperialTemplate.socialMemory = true;
                ScavengerImperialTemplate.communityID = CreatureCommunities.CommunityID.Scavengers;
                ScavengerImperialTemplate.communityInfluence = 1f;
                ScavengerImperialTemplate.dangerousToPlayer = 1.0f;

                ScavengerImperialTemplate.meatPoints = 4;
                ScavengerImperialTemplate.usesNPCTransportation = true;
                ScavengerImperialTemplate.usesRegionTransportation = true;
                ScavengerImperialTemplate.usesCreatureHoles = false;
                ScavengerImperialTemplate.jumpAction = "Point";
                ScavengerImperialTemplate.pickupAction = "Pick Up";
                ScavengerImperialTemplate.throwAction = "Throw";

                ScavengerImperialTemplate.name = "Scavenger Imperial";

                for (int i = 0; i < StaticWorld.creatureTemplates.Length; i++)
                {
                    if (StaticWorld.creatureTemplates[i] == null)
                    {
                        StaticWorld.creatureTemplates[i] = ScavengerImperialTemplate;
                        break;
                    }
                }
            }
        }

    }
}