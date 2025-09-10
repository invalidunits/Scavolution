using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            RegisterScavengerProphet();

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

            if (SECreatureEnums.ScavengerProphet != null)
            {
                if (iconData.critType == SECreatureEnums.ScavengerProphet)
                {
                    return "Kill_ScavengerKing";
                }
            }

            return orig(iconData);
        }

        public Color ScavengerJunior_CreatureSymbol_ColorOfCreature(On.CreatureSymbol.orig_ColorOfCreature orig, IconSymbol.IconSymbolData iconData)
        {
            if (SECreatureEnums.ScavengerProphet != null)
            {
                if (iconData.critType == SECreatureEnums.ScavengerProphet)
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

            if (SECreatureEnums.ScavengerProphet != null)
            {
                if (unlockID == SEMultiplayerUnlocks.ScavengerProphet)
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

                if (SECreatureEnums.ScavengerProphet != null)
                {
                    if (creatureTemplate.type == SECreatureEnums.ScavengerProphet)
                    {
                        self.abstractAI = new ScavengerAbstractAI(self.world, self);
                        self.state = new HealthState(self);
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
        
        CreatureTemplate? ScavengerJuniorTemplate = null;
        CreatureTemplate? ScavengerProphetTemplate = null;

        public void StaticWorld_InitCustomTemplates(On.StaticWorld.orig_InitCustomTemplates orig)
        {

            orig();

            Logger.LogDebug("Initializing Scavenger Junior");
            List<TileTypeResistance> tile_resistance = new List<TileTypeResistance>();
            List<TileConnectionResistance> tile_connection_resistance = new List<TileConnectionResistance>();
            ScavengerJuniorTemplate = new CreatureTemplate(SECreatureEnums.ScavengerJunior,
                StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger),
                tile_resistance,
                tile_connection_resistance,
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
            ScavengerJuniorTemplate.preBakedPathingAncestor = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.StandardGroundCreature);
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

            if (SECreatureEnums.ScavengerProphet != null)
            {
                Logger.LogDebug("Initializing Scavenger Prophet");
                ScavengerProphetTemplate = new CreatureTemplate(SECreatureEnums.ScavengerProphet,
                    StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger),
                    tile_resistance,
                    tile_connection_resistance,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0.1f)
                );

                ScavengerProphetTemplate.BlizzardWanderer = false;
                ScavengerProphetTemplate.BlizzardAdapted = false;
                ScavengerProphetTemplate.baseDamageResistance = 3.0f;
                ScavengerProphetTemplate.baseStunResistance = 1.5f;
                ScavengerProphetTemplate.instantDeathDamageLimit = 3.0f;

                ScavengerProphetTemplate.offScreenSpeed = 1.25f;
                ScavengerProphetTemplate.grasps = 4;
                ScavengerProphetTemplate.AI = true;
                ScavengerProphetTemplate.requireAImap = true;
                ScavengerProphetTemplate.abstractedLaziness = 50;
                ScavengerProphetTemplate.bodySize = 1.2f;
                ScavengerProphetTemplate.doPreBakedPathing = false;
                ScavengerProphetTemplate.preBakedPathingAncestor = StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.StandardGroundCreature);
                ScavengerProphetTemplate.stowFoodInDen = false;
                ScavengerProphetTemplate.shortcutSegments = 2;

                ScavengerProphetTemplate.visualRadius = 1000f;
                ScavengerProphetTemplate.movementBasedVision = 0.3f;

                ScavengerProphetTemplate.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
                ScavengerProphetTemplate.hibernateOffScreen = true;
                ScavengerProphetTemplate.roamBetweenRoomsChance = -1f;
                ScavengerProphetTemplate.roamInRoomChance = -1f;
                ScavengerProphetTemplate.socialMemory = true;
                ScavengerProphetTemplate.communityID = CreatureCommunities.CommunityID.Scavengers;
                ScavengerProphetTemplate.communityInfluence = 1f;
                ScavengerProphetTemplate.dangerousToPlayer = 1.0f;

                ScavengerProphetTemplate.meatPoints = 4;
                ScavengerProphetTemplate.usesNPCTransportation = true;
                ScavengerProphetTemplate.usesRegionTransportation = true;
                ScavengerProphetTemplate.usesCreatureHoles = false;
                ScavengerProphetTemplate.jumpAction = "Point";
                ScavengerProphetTemplate.pickupAction = "Pick Up";
                ScavengerProphetTemplate.throwAction = "Throw";

                ScavengerProphetTemplate.name = "Scavenger Prophet";

                for (int i = 0; i < StaticWorld.creatureTemplates.Length; i++)
                {
                    if (StaticWorld.creatureTemplates[i] == null)
                    {
                        StaticWorld.creatureTemplates[i] = ScavengerProphetTemplate;
                        break;
                    }
                }
            }
        }

    }
}