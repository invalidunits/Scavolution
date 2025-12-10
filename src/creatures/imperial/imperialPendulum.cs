using System;
using RWCustom;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Scavolution
{
    public class ImperialPendulum : SharedPhysics.IProjectileTracer
    {
        public const int FLAIL_COOLDOWN = 300;
        public int flailTimer = 0;
        public class FlailAnimation : Scavenger.ScavengerAnimation
        {
            public int slowingDown = 0;
            public int startTime = 0;
            public int timeLeft;
            public readonly int pendulumIndex;

            public ImperialPendulum? myPendulum => ScavengerBearClaws.GetClaws(scavenger)?.pendulums?[pendulumIndex];
            public override bool Continue => myPendulum?.mode == PendulumMode.Flail;
            public Vector2 flailDirection = Vector2.up;
            public FlailAnimation(Scavenger scav, int pendulumIndex) : base(scav, SEScavengerAnimations.Flail)
            {
                startTime = UnityEngine.Random.Range(100, 120);
                timeLeft = startTime;
                this.pendulumIndex = pendulumIndex;
                flailDirection = Vector2.up;
                flailDirection = GetBestFlailDir();
            }

            public Vector2 GetBestFlailDir()
            {
                if (ScavolutionPlugin.ControlledScavenger(scavenger.abstractCreature) && myPendulum is not null)
                {
                    return (Vector2)Futile.mousePosition + scavenger.room.game.cameras[0].pos - myPendulum.RestPosition;
                }

                if (scavenger.AI.preyTracker.currentPrey is PreyTracker.TrackedPrey prey)
                {
                    var bestpos = prey.critRep.BestGuessForPosition();
                    if (scavenger.room.abstractRoom.index == bestpos.room)
                    {
                        return Custom.DirVec(scavenger.mainBodyChunk.pos, scavenger.room.MiddleOfTile(bestpos));
                    }
                }

                return flailDirection;
            }
            

            public override void Update()
            {
                base.Update();
                if (myPendulum is null) return;

                timeLeft -= 1;

                bool slowdown = Mathf.Max(scavenger.AI.preyTracker.Utility(), scavenger.AI.threatTracker.Utility()) < 0.15f || timeLeft <= -20;
                if (ScavolutionPlugin.ControlledScavenger(scavenger.abstractCreature))
                {
                    slowdown = !(scavenger.inputWithoutDiagonals.HasValue && scavenger.inputWithoutDiagonals.Value.spec);
                }
                
                if (slowdown)
                {
                    ++slowingDown;
                    if (slowingDown > 40 && myPendulum.mode == PendulumMode.Flail)
                    {
                        if (scavenger.animation == this) scavenger.animation = null;
                        myPendulum.Reset();
                        return;
                    }
                }
                else
                {
                    slowingDown = Mathf.Max(slowingDown--, 0);
                }

                if (timeLeft <= 0 && timeLeft >= -20)
                {
                    ThrowCheck();
                }

  
            }

            public void ThrowCheck()
            {
                if (myPendulum is null) return;
                if (slowingDown > 0) return;

                if (ScavolutionPlugin.ControlledScavenger(scavenger.abstractCreature))
                {
                    myPendulum.Send(((Vector2)Futile.mousePosition + scavenger.room.game.cameras[0].pos - myPendulum.RestPosition) * 1.2f, true, 1.0f, false, false, false);
                    return;
                }

                float power = 1.0f - 0.7f*Mathf.Sqrt(Mathf.Clamp01(timeLeft / 160f));
                var focusCreature = scavenger.AI.focusCreature;
                if (focusCreature is null) return;
                var dynrelationship = scavenger.AI.DynamicRelationship(focusCreature);
                var statrelationship = scavenger.AI.StaticRelationship(focusCreature.representedCreature);
                if (((dynrelationship.GoForKill || dynrelationship.type == CreatureTemplate.Relationship.Type.Afraid) || (statrelationship.GoForKill || statrelationship.type == CreatureTemplate.Relationship.Type.Afraid)) && focusCreature.representedCreature.realizedCreature is not null)
                {
                    var realized = focusCreature.representedCreature.realizedCreature;
                    BodyChunk bodyChunk = realized.bodyChunks[UnityEngine.Random.Range(0, realized.bodyChunks.Length)];
                    if (focusCreature.VisualContact)
                    {
                        for (int j = 0; j < scavenger.AI.tracker.CreaturesCount; j++)
                        {
                            if (scavenger.AI.tracker.GetRep(j).dynamicRelationship.currentRelationship.type == CreatureTemplate.Relationship.Type.Pack &&
                                scavenger.AI.tracker.GetRep(j).representedCreature.realizedCreature != null &&
                                !scavenger.AI.tracker.GetRep(j).representedCreature.realizedCreature.dead &&
                                scavenger.AI.tracker.GetRep(j).representedCreature.realizedCreature.room == scavenger.room &&
                                Custom.DistLess(scavenger.AI.tracker.GetRep(j).representedCreature.realizedCreature.mainBodyChunk.pos,
                                    Custom.ClosestPointOnLineSegment(myPendulum.RestPosition,
                                    bodyChunk.pos, scavenger.AI.tracker.GetRep(j).representedCreature.realizedCreature.mainBodyChunk.pos), 40f))
                            {
                                if (scavenger.AI.tracker.GetRep(j).representedCreature.realizedCreature is Scavenger scav)
                                {
                                    if (JuniorOnBack.onback_map.TryGetValue(scav, out var onback) && onback.owner == myPendulum.owner)
                                    {
                                        continue;
                                    }
                                }

                                Custom.Log("return, friend in the way");
                                return;
                            }
                        }

                        Vector2 direction = bodyChunk.lastLastPos - myPendulum.RestPosition;
                        myPendulum.Send(Custom.rotateVectorDeg(direction, UnityEngine.Random.Range(-3, 3)*(
                                Mathf.Min(1.0f - scavenger.midRangeSkill, 0f))), true, power, false, false, false);
                        if (scavenger.animation == this) scavenger.animation = null;
                    }
                }
            }
        }

        public class PendulumMode : ExtEnum<PendulumMode>
        {
            public static readonly PendulumMode Rest = new PendulumMode(nameof(Rest), true);
            public static readonly PendulumMode Retracting = new PendulumMode(nameof(Retracting), true);
            public static readonly PendulumMode Attached = new PendulumMode(nameof(Attached), true);
            public static readonly PendulumMode Flail = new PendulumMode(nameof(Flail), true);
            public PendulumMode(string value, bool register = false)
                : base(value, register)
            {

            }
        }

        public class PendulumSoundEmitter : PositionedSoundEmitter
        {
            public ImperialPendulum pendulum;

            public PendulumSoundEmitter(ImperialPendulum pendulum, float vol, float ptch)
                : base(pendulum.AbsolutePosition, vol, ptch)
            {
                this.pendulum = pendulum;
            }

            public override void Update(bool eu)
            {
                lastPos = pos;
                pos = pendulum.AbsolutePosition;
                if (pendulum.owner.room != room)
                {
                    alive = false;
                }

                base.Update(eu);
            }
        }

        public class PendulumSoundLoop : DynamicSoundLoop
        {
            public ImperialPendulum pendulum;
            public PendulumSoundLoop(ImperialPendulum pendulum)
                : base(pendulum.owner)
            {
                this.pendulum = pendulum;
            }

            public override void InitSound()
            {
                this.emitter = new PendulumSoundEmitter(pendulum, v, p);
                pendulum.owner.room.PlaySound(sound, (PositionedSoundEmitter)emitter, true, v, p, true);
            }
        }

        private static ConditionalWeakTable<PhysicalObject, HashSet<ImperialPendulum>> _connectedPendulums = new();
        public static HashSet<ImperialPendulum> getStuckPendulums(PhysicalObject obj) => _connectedPendulums.GetValue(obj, x => new HashSet<ImperialPendulum>());



        public PendulumMode mode = PendulumMode.Rest;


        public Vector2 rotation;
        public Vector2 absoluteAttachedPosition;

        public Vector2 relativeLastFlailPosition = Vector2.zero;
        public float flailMomentum = 0f;
        public float flailRotation = 0f;



        public Vector2 retractingVelocity;
        public Vector2 retractingAbsolutelastPosition;
        public Vector2 retractingRelativePosition;

        public Vector2 AbsoluteLastPosition
        {
            get
            {
                if (mode == PendulumMode.Attached)
                {
                    if (this.stuckInAppendage is not null)
                    {
                        var appendage = this.stuckInAppendage.appendage;
                        return Vector2.Lerp(appendage.segments[stuckInAppendage.prevSegment], appendage.segments[stuckInAppendage.prevSegment + 1], stuckInAppendage.distanceToNext);
                    }

                    if (this.stuckInObject is not null)
                    {
                        var chunk = this.stuckInObject.bodyChunks[this.stuckInChunkIndex];
                        return chunk.lastPos + -rotation.normalized * chunk.rad;
                    }

                    return absoluteAttachedPosition;
                }

                if (mode == PendulumMode.Retracting)
                {
                    return retractingAbsolutelastPosition;
                }

                if (mode == PendulumMode.Flail)
                {
                    return relativeLastFlailPosition + RestPosition;
                }

                return LastRestPosition;
            }
        }

        public Vector2 AbsolutePosition
        {
            get
            {
                if (mode == PendulumMode.Attached)
                {
                    if (this.stuckInAppendage is not null)
                    {
                        var appendage = this.stuckInAppendage.appendage;
                        return Vector2.Lerp(appendage.segments[stuckInAppendage.prevSegment], appendage.segments[stuckInAppendage.prevSegment + 1], stuckInAppendage.distanceToNext);;
                    }
                    
                    if (this.stuckInObject is not null)
                    {
                        var chunk = this.stuckInObject.bodyChunks[this.stuckInChunkIndex];
                        return chunk.pos + -rotation.normalized * chunk.rad;
                    }
                    return absoluteAttachedPosition;
                }

                if (mode == PendulumMode.Retracting)
                {
                    return RestPosition + retractingRelativePosition;
                }

                if (mode == PendulumMode.Flail)
                {
                    Vector2 flailDirection = Vector2.right;
                    float squish = 0.5f;
                    var circle = new Vector2(Mathf.Cos(flailRotation), Mathf.Sin(flailRotation));
                    circle.y *= squish;
                    circle.x *= 1f / squish;

                    float slowdown = 0f;
                    if (owner is Scavenger scav && scav.animation is FlailAnimation flail)
                    {
                        slowdown = ((float)flail.slowingDown) / 40f;
                    }

                    circle *= Mathf.Lerp(Mathf.Lerp(0f, 8 * 20f, (float)ticksSinceModeChange / 200f), 0, slowdown);
                    return RestPosition + circle;
                }

                return RestPosition;
            }
        }


        public Vector2 offset = Vector2.up * 20f;
        public Vector2 LastRestPosition
        {
            get
            {
                return connectedChunk.lastPos + offset;
            }
        }

        public Vector2 RestPosition
        {
            get
            {
                return connectedChunk.pos + offset;
            }
        }




        public int ticksSinceModeChange = 0;
        public PhysicalObject? stuckInObject = null;
        public int stuckInChunkIndex = -1;



        public PhysicalObject.Appendage.Pos? stuckInAppendage = null;


        public BodyChunk connectedChunk;
        public PhysicalObject owner => connectedChunk.owner;
        public bool IsAttached => mode == PendulumMode.Attached;
        public bool IsAttachedToWall => mode == PendulumMode.Attached && stuckInObject is null;
        public float attachedLength = 0f;
        public float ropeLength = MAX_ROPELENGTH;
        public bool retractImmidietly = false;
        public const float MAX_ROPELENGTH = 7 * 20f;
        public const float MIN_ROPELENGTH = 4 * 20f;
        public readonly int pendulumIndex;
        public PendulumSoundLoop AirSoundLoop;
        public List<StaticSoundLoop> ResonatorSoundLoop;
        public ImperialPendulum(BodyChunk chunk, int pendulumIndex)
        {
            this.connectedChunk = chunk;
            this.pendulumIndex = pendulumIndex;
            this.AirSoundLoop = new(this);
            this.ResonatorSoundLoop = new();
            Reset();
        }

        public void Reset()
        {
            mode = PendulumMode.Rest;
            ticksSinceModeChange = 0;
            if (stuckInObject != null) getStuckPendulums(stuckInObject).Remove(this);
            stuckInObject = null;
            stuckInChunkIndex = -1;
            stuckInAppendage = null;
            attachedLength = 0f;
            flailMomentum = 0f;
            ropeLength = MAX_ROPELENGTH;
            retractImmidietly = false;
        }

        public void Release()
        {
            if (mode == PendulumMode.Attached)
            {
                if (stuckInObject != null)
                {
                    owner.room.PlaySound(SoundID.Spear_Dislodged_From_Creature, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                }
                else
                {
                    owner.room.PlaySound(SoundID.Slugcat_Pick_Up_Spear, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                }

            }

            retractingVelocity = Vector2.zero;
            retractingRelativePosition = AbsolutePosition - RestPosition;
            retractingAbsolutelastPosition = AbsoluteLastPosition;
            mode = PendulumMode.Retracting;
            ticksSinceModeChange = 0;
            attachedLength = 0f;
            flailMomentum = 0f;
            retractImmidietly = false;

            if (stuckInObject != null) getStuckPendulums(stuckInObject).Remove(this);
            stuckInObject = null;
            stuckInChunkIndex = -1;
            stuckInAppendage = null;
        }

        public void Update(bool eu)
        {
            ticksSinceModeChange++;
            if (mode == PendulumMode.Attached)
            {
                if (retractImmidietly)
                {
                    ropeLength = 0f; // retract immidietly.
                }

                AirSoundLoop.sound = SoundID.None;
                if (stuckInObject != null)
                {

                    if ((stuckInObject.slatedForDeletetion || owner.room != stuckInObject.room || !owner.room.updateList.Contains(stuckInObject)))
                    {
                        Release();
                    }
                    else
                    {
                        foreach (Creature.Grasp grasp in stuckInObject.grabbedBy.ToList()) 
                        {
                            if (grasp.grabber is Scavenger && owner is Creature critter)
                            {
                                owner.room.socialEventRecognizer.Theft(grasp.grabbed, critter, grasp.grabber);
                            }

                            grasp.Release();
                        }
                        
                        if (stuckInObject is PoleMimic mimic)
                        {
                            mimic.angeredAndAggressive = Mathf.Max(mimic.angeredAndAggressive, 80);
                        }

                        if (stuckInObject is Weapon weapon)
                        {
                            weapon.ChangeMode(Weapon.Mode.Free);
                        }

                        if (stuckInObject is SporePlant plant)
                        {
                            if (plant.stalk != null)
                            {
                                plant.stalk.sporePlant = null;
                                if (!plant.AbstrSporePlant.isConsumed) plant.AbstrSporePlant.Consume();
                            }
                        }

                        if (stuckInObject is DangleFruit fruit)
                        {
                            if (fruit.stalk != null)
                            {
                                fruit.stalk.fruit = null;
                                if (!fruit.AbstrConsumable.isConsumed) fruit.AbstrConsumable.Consume();
                            }
                        }
                        
                        foreach (Player.AbstractOnBackStick stick in stuckInObject.abstractPhysicalObject.stuckObjects.OfType<Player.AbstractOnBackStick>().ToList())
                        {
                            if (stick.A.realizedObject is Player p)
                            {
                                if (p.spearOnBack?.spear == stuckInObject)
                                {
                                    p.spearOnBack.DropSpear();
                                }

                                stick.Deactivate();
                            }
                        }
                        
                    }
                }
    
                Elasticity(eu);
            }

            if (mode == PendulumMode.Retracting)
            {
                AirSoundLoop.sound = SoundID.Rock_Through_Air_LOOP;
                AirSoundLoop.Volume = Mathf.InverseLerp(5f, 15f, retractingVelocity.magnitude);
                AirSoundLoop.Update();

                retractingAbsolutelastPosition = AbsolutePosition;
                Vector2 lastRelativePosition = retractingRelativePosition;

                retractingVelocity -= retractingRelativePosition.normalized * 2.2f * new Vector2(1, 0);
                retractingVelocity -= Math.Sign(retractingRelativePosition.y) * owner.room.gravity * 2.0f * Vector2.up;
                retractingRelativePosition += retractingVelocity;
                retractingRelativePosition = Vector2.Lerp(retractingRelativePosition, Vector2.zero, 0.1f);
                if (retractingRelativePosition.sqrMagnitude < 5f*5f)
                {
                    Reset();
                }
            }
            


            for (int i = ResonatorSoundLoop.Count - 1; i >= 0; i--)
            {
                ResonatorSoundLoop[i].volume = Mathf.MoveTowards(ResonatorSoundLoop[i].volume, 0f, 0.01f);
                ResonatorSoundLoop[i].Update();

                if (ResonatorSoundLoop[i].room != owner.room || ResonatorSoundLoop[i].emitter == null)
                {
                    ResonatorSoundLoop.Remove(ResonatorSoundLoop[i]);
                }
            }

            if (mode == PendulumMode.Flail)
            {
                flailTimer = FLAIL_COOLDOWN;
                relativeLastFlailPosition = AbsolutePosition - RestPosition;
                AirSoundLoop.sound = SoundID.Rock_Through_Air_LOOP;
                AirSoundLoop.Volume = 1.0f;
                AirSoundLoop.Update();

                if (owner is Scavenger scavowner)
                {
                    if (scavowner.animation is not FlailAnimation flail || flail.pendulumIndex != this.pendulumIndex)
                    {
                        Reset();
                    }
                    else
                    {
                        flailMomentum = Mathf.Lerp(flailMomentum, 8f * Mathf.PI * 2f / 40f, 0.05f);
                        flailRotation += flailMomentum;
                    }
                }


            }
            else
            {
                flailTimer--;
            }
            
            if (mode == PendulumMode.Rest)
            {
                AirSoundLoop.sound = SoundID.None;
            }
        }
        public bool HitThisObject(PhysicalObject obj)
        {
            if (obj is Scavenger scav)
            {
                if (JuniorOnBack.onback_map.TryGetValue(scav, out var onback) && onback.owner == owner)
                {
                    return false;
                }
            }
            
            if (obj == owner) return false;
            return (retractImmidietly || obj.canBeHitByWeapons) && obj.abstractPhysicalObject.IsSameRippleLayer(owner.abstractPhysicalObject.rippleLayer);

        }

        public bool HitThisChunk(BodyChunk chunk)
        {
            return true;
        }


        public static IntVector2? RayTraceTilesForTerrainReturnFirstSolidOrCeiling(Room room, Vector2 A, Vector2 B)
        {
            float num = A.x / 20f;
            float num2 = A.y / 20f;
            float num3 = B.x / 20f;
            float num4 = B.y / 20f;
            float num5 = Mathf.Abs(num3 - num);
            float num6 = Mathf.Abs(num4 - num2);
            int num7 = Mathf.FloorToInt(num);
            int num8 = Mathf.FloorToInt(num2);
            float num9 = 1f / num5;
            float num10 = 1f / num6;
            int num11 = 1;
            int num12;
            float num13;
            if (num5 == 0f)
            {
                num12 = 0;
                num13 = num9;
            }
            else if (num3 > num)
            {
                num12 = 1;
                num11 += Mathf.FloorToInt(num3) - num7;
                num13 = ((float)(Mathf.FloorToInt(num) + 1) - num) * num9;
            }
            else
            {
                num12 = -1;
                num11 += num7 - Mathf.FloorToInt(num3);
                num13 = (num - (float)Mathf.FloorToInt(num)) * num9;
            }

            int num14;
            float num15;
            if (num6 == 0f)
            {
                num14 = 0;
                num15 = num10;
            }
            else if (num4 > num2)
            {
                num14 = 1;
                num11 += Mathf.FloorToInt(num4) - num8;
                num15 = ((float)(Mathf.FloorToInt(num2) + 1) - num2) * num10;
            }
            else
            {
                num14 = -1;
                num11 += num8 - Mathf.FloorToInt(num4);
                num15 = (num2 - (float)Mathf.FloorToInt(num2)) * num10;
            }

            while (num11 > 0)
            {
                if (room.HasAnySolid(num7, num8) ||
                    ((room.CameraViewingPoint(room.MiddleOfTile(num7, num8)) == -1) &&
                     (room.CameraViewingPoint(room.MiddleOfTile(num7, num8 - 1)) == -1) && 
                     (room.CameraViewingPoint(room.MiddleOfTile(num7, num8 - 2)) != -1)))
                {
                    return new IntVector2(num7, num8);
                }

                if (num15 < num13)
                {
                    num8 += num14;
                    num15 += num10;
                }
                else
                {
                    num7 += num12;
                    num13 += num9;
                }

                num11--;
            }

            return null;
        }

        public void Send(Vector2 directionAndLength, bool hitBodyChunks = true, float flailCharge = 0f, bool pacifist = false, bool grab = false, bool attach = true, bool hitAppendages = false)
        {
            if (owner.room is null) return;
            owner.room.PlaySound(SoundID.Slugcat_Throw_Rock, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
            Reset();
            priorityPull = 0;
            ticksSinceModeChange = 0;
            Vector2 rayorigin = RestPosition;
            Vector2 raydest = rayorigin + directionAndLength;
            if (owner is Creature critter)
            {
                var aiTile = critter.room.aimap.getAItile(critter.room.GetTilePosition(critter.mainBodyChunk.pos));
                if (aiTile.narrowSpace)
                {
                    rayorigin = Vector2.MoveTowards(rayorigin, raydest, 19f);
                }
            }
            

            // Vector2? rayresult = SharedPhysics.ExactTerrainRayTracePos(owner.room, rayorigin, raydest);
            Vector2? exactRayResult;
            Vector2 normal = default;
            FloatRect? raytrace = null;
            if (RayTraceTilesForTerrainReturnFirstSolidOrCeiling(owner.room, rayorigin, raydest) is IntVector2 vec)
            {
                raytrace = Custom.RectCollision(raydest, rayorigin, owner.room.TileRect(vec));
            }


            if (!raytrace.HasValue)
            {
                exactRayResult = null;
            }
            else
            {
                exactRayResult = new Vector2(raytrace.Value.left, raytrace.Value.bottom);
                normal = -new Vector2(raytrace.Value.right, raytrace.Value.top);
            }
            
            int stablility_chunk = -1;
            if (owner is Scavenger)
            {
                stablility_chunk = 1;
            }

            Vector2 followThrough = directionAndLength.normalized * (Mathf.Pow(directionAndLength.magnitude, 0.25f) / connectedChunk.mass);
            if (stablility_chunk > 0)
            {
                owner.WeightedPush(connectedChunk.index, stablility_chunk, followThrough, 1f);
            }
            else
            {
                connectedChunk.vel += followThrough;
            }
            this.retractImmidietly = grab;

            SharedPhysics.CollisionResult result = new(null, null, null, exactRayResult is not null, exactRayResult ?? default);
            if (hitBodyChunks)
            {
                Vector2 raydest2 = exactRayResult ?? raydest;
                var result2 = SharedPhysics.TraceProjectileAgainstBodyChunks(this, owner.room, rayorigin, ref raydest2, grab? 10f : 6f, owner.collisionLayer, owner, hitAppendages);
                if (result2.hitSomething)
                {
                    result = result2;
                    normal = (raydest2 - result.collisionPoint).normalized;
                }

                if (grab)
                {
                    var result3 = SharedPhysics.TraceProjectileAgainstBodyChunks(this, owner.room, rayorigin, ref raydest2, grab? 10f : 6f, 2, owner, hitAppendages); // check for weapons
                    if (result3.hitSomething)
                    {
                        result = result3;
                        normal = (raydest2 - result.collisionPoint).normalized;
                    }
                }

            }

            if (result.hitSomething)
            {
                flailCharge = Mathf.Clamp01(flailCharge);
                float flailDamage = Mathf.Lerp(1f, 10f, flailCharge);
                if (pacifist)
                {
                    flailDamage = 0f;
                    flailCharge = 0f;
                }

                if (flailCharge > 0f)
                {
                    attach = false;
                }

                int rings = flailCharge > 0.8f ? 3 : 0;
                for (int i = 0; i < rings; i++)
                {
                    Vector2 initial = Vector2.Lerp(rayorigin, result.collisionPoint, 0.25f);
                    Vector2 end = Vector2.Lerp(rayorigin, result.collisionPoint, 0.8f);
                    var ringPos = Vector2.Lerp(initial, end, (i / (rings - 1)) + UnityEngine.Random.Range(-0.1f, 0.1f));
                    owner.room.AddObject(new SmokeRing(ringPos,
                        -directionAndLength.normalized * 10f, directionAndLength.normalized));
                }

                if (flailCharge > 0.25f)
                {
                    owner.room.AddObject(new Explosion.ExplosionLight(result.collisionPoint, 80, 1f, 3, Color.white));
                    owner.room.ScreenMovement(result.collisionPoint, directionAndLength.normalized * (result.chunk is null ? 20f : 40f), 0.4f);
                    owner.room.AddObject(new ShockWave(result.collisionPoint, 330f, 0.045f, 5));
                }
                else
                {
                    owner.room.AddObject(new Explosion.ExplosionLight(result.collisionPoint, 60, 0.5f, 3, Color.white));
                }


                if (flailCharge > 0.5f)
                {
                    ResonatorSoundLoop.Add(new StaticSoundLoop(SoundID.Deaf_Sine_LOOP, result.collisionPoint, owner.room, flailCharge * 1.5f, 1.0f));
                }

                if (flailCharge > 0.5f && result.obj is Centipede centipede)
                {
                    centipede.shellJustFellOff = result.chunk!.index;
                    List<int> removedShells = new List<int>(3) { result.chunk!.index };
                    if (result.chunk!.index >= 0)
                    {
                        removedShells.Add(result.chunk!.index - 1);
                    }


                    if (result.chunk!.index < (centipede.CentiState.shells.Length - 1))
                    {
                        removedShells.Add(result.chunk!.index + 1);
                    }


                    for (int i = 0; i < removedShells.Count; i++)
                    {
                        centipede.CentiState.shells[removedShells[i]] = false;
                        if (centipede.graphicsModule != null)
                        {

                            for (int j = 0; j < ((!centipede.Red) ? 1 : 3); j++)
                            {
                                CentipedeShell centipedeShell = new CentipedeShell(centipede.bodyChunks[i].pos,
                                    directionAndLength.normalized * 5f * flailDamage * Mathf.Lerp(0.7f, 1.6f, UnityEngine.Random.value) + Custom.RNV() * UnityEngine.Random.value * ((j == 0) ? 3f : 6f),
                                    (centipede.graphicsModule as CentipedeGraphics)!.hue, (centipede.graphicsModule as CentipedeGraphics)!.saturation,
                                    centipede.bodyChunks[i].rad * 1.8f * (1f / 14f) * 1.2f, centipede.bodyChunks[i].rad * 1.3f * (1f / 11f) * 1.2f);
                                if (centipede.abstractCreature.IsVoided())
                                {
                                    centipedeShell.lavaImmune = true;
                                }

                                centipede.room.AddObject(centipedeShell);
                            }
                        }

                        if (centipede.Red)
                        {
                            centipede.room.PlaySound(SoundID.Red_Centipede_Shield_Falloff, centipede.bodyChunks[i]);
                        }
                    }
                }
                HitSomething(result, directionAndLength, normal, flailDamage, attach);
            }
            else
            {
                Release();
                retractingRelativePosition = directionAndLength;
                retractingAbsolutelastPosition = AbsolutePosition;
            }
        }

        public AbstractCreature NotSlugcatPlayableCritterCredit(AbstractCreature s)
        {
            if (SprobDesecratingGraves.CreatureHooks.GetCreatureData(s).controller is Player controller)
            {
                return controller.abstractCreature;
            }

            return s;
        }


        private void PendulumHitObject(ref bool attach, float damageMultiplier, ref SharedPhysics.CollisionResult collision)
        {
            if (stuckInObject is null) return;
            if (stuckInObject is DaddyLongLegs)
            {
                damageMultiplier *= 50;
            }

            if (ModManager.Watcher && stuckInObject is Watcher.Loach loach)
            {
                loach.Die();
            }

            BodyChunk? chunk = stuckInAppendage == null? stuckInObject.bodyChunks[stuckInChunkIndex] : null;
            Vector2 forceDirection = (collision.collisionPoint - RestPosition).normalized;
            float decreasingMultiplier = damageMultiplier > 1f ? Mathf.Pow(damageMultiplier, 0.7f) : damageMultiplier;
            Vector2 force = forceDirection * 20f * decreasingMultiplier;

            bool violence = damageMultiplier > 0;
            if (chunk != null)
            {
                chunk.pos += forceDirection * 2f / chunk.mass;
            }
            

            if (stuckInObject is Creature critter)
            {
                priorityPull = 0.5f;
                if (critter is Scavenger scav && scav.isJunior())
                {
                    priorityPull = 0.9f;
                }

                if (owner is Creature owner_critter)
                {
                    critter.SetKillTag(ScavolutionPlugin.NotSlugcatPlayables? NotSlugcatPlayableCritterCredit(owner_critter.abstractCreature) : owner_critter.abstractCreature);
                }

                if (chunk != null && critter is Lizard lizard)
                {
                    if (chunk.index == 0 && (!attach || lizard.HitHeadShield(forceDirection)))
                    {
                        owner.room.AddObject(new Spark(collision.collisionPoint, Custom.RNV() * 60f * UnityEngine.Random.value, Color.white, null, 20, 50));
                        if (!attach) owner.room.PlaySound(SoundID.Lizard_Head_Shield_Deflect, lizard.mainBodyChunk);
                        if (violence) critter.Violence(null, null, chunk, null, Creature.DamageType.Blunt, 0.3f * damageMultiplier, 60 * decreasingMultiplier);
                        chunk.vel += force / chunk.mass;

                        if (lizard.abstractCreature.creatureTemplate.type != CreatureTemplate.Type.RedLizard)
                        {
                            lizard.turnedByRockDirection = (int)Mathf.Sign(forceDirection.x);
                            if (damageMultiplier > 0f)
                            {
                                lizard.turnedByRockCounter = (int)(20f * (decreasingMultiplier + 1f));
                            }
                        }

                        ScavolutionPlugin.pubLogger?.LogDebug("attach = false 1");
                        attach = false;
                    }
                    else if (chunk.index == 0 && lizard.HitInMouth(forceDirection))
                    {
                        chunk.vel += force / chunk.mass;
                        owner.room.PlaySound(SoundID.Spear_Stick_In_Creature, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                        if (violence) critter.Violence(null, null, chunk, null, Creature.DamageType.Stab, 1.4f * damageMultiplier, 60 * decreasingMultiplier);
                    }


                }
                else if (attach && critter.SpearStick(null, 0.7f, chunk, stuckInAppendage, forceDirection))
                {
                    owner.room.AddObject(new WaterDrip(collision.collisionPoint, -forceDirection * 20f * UnityEngine.Random.value * 0.5f + Custom.DegToVec(360f * UnityEngine.Random.value) * forceDirection * 20f * UnityEngine.Random.value * 0.5f, waterColor: false));
                    if (violence) critter.Violence(null, null, chunk, null, Creature.DamageType.Stab, 0.7f * damageMultiplier, 60 * decreasingMultiplier);
                    if (critter.State is PlayerState pstate)
                    {
                        pstate.permanentDamageTracking += 0.7f * damageMultiplier;
                        if (pstate.permanentDamageTracking > 1.0f) critter.Die();
                    }
                    
                    if (chunk != null)
                    {
                        chunk.vel += force / chunk.mass;
                    }

                    if (stuckInAppendage != null)
                    {
                        stuckInAppendage.appendage.ownerApps.ApplyForceOnAppendage(stuckInAppendage, force);
                    }
                    
                }
                else
                {
                    owner.room.PlaySound(SoundID.Spear_Bounce_Off_Creauture_Shell, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                    owner.room.AddObject(new Spark(collision.collisionPoint, Custom.RNV() * 60f * UnityEngine.Random.value, Color.white, null, 20, 50));
                    float forcem = 1f;
                    if (critter is Player p && p.rollCounter > 0 && p.isGourmand)
                    {
                        forcem = 0.2f;
                    }
                    else
                    {
                        if (violence) critter.Violence(null, null, chunk, null, Creature.DamageType.Blunt, 0.7f * damageMultiplier, 45 * decreasingMultiplier);
                    }
                    if (chunk != null)
                    {
                        chunk.vel += (force / chunk.mass) * forcem;
                    }

                    if (stuckInAppendage != null)
                    {
                        stuckInAppendage.appendage.ownerApps.ApplyForceOnAppendage(stuckInAppendage, force * forcem);
                    }

                    
                    attach = false;
                }
            }
            else
            {
                if (stuckInObject is PlayerCarryableItem)
                {
                    priorityPull = 1.0f;
                }

                if (chunk != null)
                {
                    chunk.vel += (force / chunk.mass) * 3f;
                }

                if (stuckInAppendage != null)
                {
                    stuckInAppendage.appendage.ownerApps.ApplyForceOnAppendage(stuckInAppendage, force * 3f);
                }
            }

            if (violence && attach)
            {
                owner.room.PlaySound(SoundID.Spear_Stick_In_Creature, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
            }
            else
            {
                owner.room.PlaySound(SoundID.Slugcat_Pick_Up_Rock, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
            }
        }

        public void HitSomething(SharedPhysics.CollisionResult collision, Vector2 directionAndLength, Vector2 normal, float damageMultiplier = 1.0f, bool attach = true)
        {
            priorityPull = 0;
            ticksSinceModeChange = 0;
            

            attachedLength = Custom.Dist(AbsolutePosition, RestPosition);
            if (collision.chunk is null && collision.onAppendagePos is null)
            {
                if (!attach)
                {
                    owner.room.PlaySound(SoundID.Spear_Bounce_Off_Creauture_Shell, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                }
                else
                {
                    owner.room.PlaySound(SoundID.Spear_Stick_In_Wall, new PendulumSoundEmitter(this, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                }

                absoluteAttachedPosition = collision.collisionPoint;
                owner.room.AddObject(new Spark(collision.collisionPoint, Custom.RNV() * 60f * UnityEngine.Random.value,
                    Color.black, null, 20, 50));
            }
            else
            {
                stuckInObject = collision.obj;
                if (collision.chunk is not null)
                {
                    stuckInChunkIndex = collision.chunk.index;
                }
                else
                {
                    stuckInAppendage = collision.onAppendagePos;
                }
                
                getStuckPendulums(stuckInObject).Add(this);
                PendulumHitObject(ref attach, damageMultiplier, ref collision);
            }



            mode = PendulumMode.Attached;
            if (!attach)
            {
                Release();

                // reflect
                var bounceVel = directionAndLength.normalized * 30f;
                var bounceVelNormalComp = Vector2.Dot(bounceVel, normal);
                bounceVel -= normal * bounceVelNormalComp;
                bounceVelNormalComp = Math.Abs(bounceVelNormalComp);
                bounceVel += normal * bounceVelNormalComp;

                retractingVelocity = bounceVel;
            }
            else
            {
                if (owner is Scavenger scavowner && ScavengerBearClaws.GetClaws(scavowner) is ScavengerBearClaws claws)
                {
                    claws.lastAttachedPendulum = this;
                }
            }
        }

        public void BeginFlail()
        {
            Reset();
            flailMomentum = 0f;
            flailRotation = UnityEngine.Random.Range(0f, Mathf.PI*2);
            ticksSinceModeChange = 0;
            relativeLastFlailPosition = Vector2.zero;
            if (owner is Scavenger scav)
            {
                scav.animation = new ImperialPendulum.FlailAnimation(scav, pendulumIndex);
            }

            mode = PendulumMode.Flail;
        }


        public float priorityPull = 0f;
        public void Elasticity(bool eu)
        {
            var difference = RestPosition - AbsolutePosition;
            if (difference.sqrMagnitude > ropeLength * ropeLength)
            {
                float ourWeight;
                BodyChunk? stuckInChunk = null; 
                if (stuckInAppendage is null)
                {
                    stuckInChunk = stuckInObject?.bodyChunks[stuckInChunkIndex];
                    ourWeight = stuckInObject is null ? 0f : owner.TotalMass / (owner.TotalMass + stuckInObject.TotalMass);
                }
                else
                {
                    ourWeight = 0.8f;
                }
                

                if (priorityPull > 0f)
                {
                    ourWeight = Mathf.Lerp(ourWeight, 1.0f, priorityPull);
                }
                else if (priorityPull < 0f)
                {
                    ourWeight = Mathf.Lerp(ourWeight, 0.0f, -priorityPull);
                }

                float stuckWeight = 1.0f - ourWeight;
                Vector2 correctionDir = -difference.normalized;
                float stretch = difference.magnitude - ropeLength;

                float ourOpposingVel = Vector2.Dot(connectedChunk.vel, -correctionDir);
                if (ourOpposingVel < 0) ourOpposingVel = 0;

                float stuckOpposingVel = stuckInChunk is null ? 0f : Vector2.Dot(stuckInChunk.vel, -correctionDir);
                if (stuckOpposingVel < 0) ourOpposingVel = 0;

                float totalOpposingVel = ourOpposingVel * ourWeight + stuckOpposingVel * stuckWeight;

                Vector2 addvel = Vector2.zero;
                addvel += ourOpposingVel * correctionDir; // remove existing opposing vel
                addvel -= totalOpposingVel * correctionDir; // replace it with total opposing vel as a momentum union.
                for (int i = 0; i < owner.bodyChunks.Length; i++)
                {
                    owner.bodyChunks[i].vel += addvel;
                }

                if (stuckInChunk is not null)
                {
                    stuckInChunk.vel += stuckOpposingVel * correctionDir; // remove existing opposing vel
                    stuckInChunk.vel -= totalOpposingVel * correctionDir; // replace it with total opposing vel as a momentum union.
                }
                
                if (stuckInAppendage is not null)
                {
                    stuckInAppendage.appendage.ownerApps.ApplyForceOnAppendage(stuckInAppendage, stuckOpposingVel * correctionDir - totalOpposingVel * correctionDir);
                }

                float positionalRestraint = 0.1f;
                connectedChunk.pos += correctionDir * stretch * stuckWeight * positionalRestraint;
                if (stuckInChunk != null)
                    stuckInChunk.pos -= correctionDir * stretch * ourWeight * positionalRestraint;

                float forceRestraint = 0.01f;
                connectedChunk.vel += correctionDir * stretch * stuckWeight * forceRestraint;
                if (stuckInChunk != null)
                    stuckInChunk.vel -= correctionDir * stretch * ourWeight * forceRestraint;
            }
        }
    }

    public class SmokeRing : CosmeticSprite
    {
        public int time = 0;
        public Vector2 direction;
        public float initialSize = 20f * 1f;
        public float finalSize = 20f * 3f;
        public int lifeTime = 40;
        int parts = 40;
        public float squish = 1.1f;
        public float thickness = 3f;
        public SmokeRing(Vector2 position, Vector2 velocity, Vector2 direction, float initialSize = 0f, float finalSize = 40f, int lifeTime = 40, int parts = 40, float squish = 1.1f, float thickness = 1f)
        {
            this.pos = position;
            this.vel = velocity;
            this.direction = direction.normalized;
            this.initialSize = initialSize;
            this.finalSize = finalSize;
            this.lifeTime = lifeTime;
            this.parts = parts;
            this.squish = squish;
            this.thickness = thickness;
            this.time = 0;
        }


        public override void Update(bool eu)
        {
            base.Update(eu);
            time += 1;

            float life = (float)time / (float)lifeTime;
            if (life >= 1f)
            {
                Destroy();
            }
        }


        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];
            sLeaser.sprites[0] = TriangleMesh.MakeLongMesh(parts, pointyTip: false, customColor: true);
            AddToContainer(sLeaser, rCam, null!);
            base.InitiateSprites(sLeaser, rCam);
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
            if (sLeaser.deleteMeNextFrame) return;

            float life = Mathf.Clamp01((float)time / (float)lifeTime);
            Vector2 currentPartPos(int index)
            {
                float t = (float)index / ((float)parts - 1);

                float a = Mathf.Pow(2f, squish);
                float b = 1f / a;
                

                float rev = t * Mathf.PI * 2f;
                float size = Mathf.Lerp(initialSize, finalSize, Mathf.Pow(life, 0.5f));

                Vector2 circle = size * new Vector2(a*Mathf.Cos(rev), b*Mathf.Sin(rev));
                return pos + (circle.x*Vector2.Perpendicular(direction) + circle.y*direction);
            }
            
            var triMesh = (TriangleMesh)sLeaser.sprites[0];
            triMesh.alpha = Mathf.Pow(1.0f - life, 2f);
            triMesh.SetPosition(Vector2.zero);
            for (int i = 0; i < parts; i++)
            {

                int partIndex = i * 4;
                Vector2 partPos = currentPartPos(i);
                if (i == 0)
                {
                    Vector2 followingPartPos = currentPartPos(i + 1);
                    Vector2 normalized = Custom.DirVec(partPos, followingPartPos);
                    Vector2 perpendicularVec = Vector2.Perpendicular(normalized);
                    triMesh.MoveVertice(partIndex + 0, (partPos - perpendicularVec * thickness) - camPos);
                    triMesh.MoveVertice(partIndex + 1, (partPos + perpendicularVec * thickness) - camPos);
                    triMesh.MoveVertice(partIndex + 2, (partPos - perpendicularVec * thickness) - camPos);
                    triMesh.MoveVertice(partIndex + 3, (partPos + perpendicularVec * thickness) - camPos);
                }
                else
                {
                    Vector2 formerPartPos = currentPartPos(Math.Max(i - 1, 0));
                    Vector2 normalized = Custom.DirVec(formerPartPos, partPos);
                    Vector2 perpendicularVec = Vector2.Perpendicular(normalized);
                    triMesh.MoveVertice(partIndex + 0, (partPos - perpendicularVec * thickness) - camPos);
                    triMesh.MoveVertice(partIndex + 1, (partPos + perpendicularVec * thickness) - camPos);
                    triMesh.MoveVertice(partIndex + 2, (partPos - perpendicularVec * thickness) - camPos);
                    triMesh.MoveVertice(partIndex + 3, (partPos + perpendicularVec * thickness) - camPos);
                }
            }
        }

    }

    public class ImperialPendulumGraphics : ComplexGraphicsModule.GraphicsSubModule
    {
        int pendulumIndex;
        ImperialPendulum pendulum;
        ScavengerGraphics scavGraphics => (ScavengerGraphics)owner;
        // FLabel modeLabel;
        Color blackColor = Color.black;

        int parts = 40;
        float thickness = 1.0f;

        public ImperialPendulumGraphics(ImperialPendulum pendulum, int pendulumIndex, ComplexGraphicsModule owner, int firstSprite) : base(owner, firstSprite)
        {
            this.totalSprites = 1;
            this.pendulumIndex = pendulumIndex;
            this.pendulum = pendulum;
            // this.modeLabel = new FLabel(Custom.GetFont(), "");
        }

        // public override void Update()
        // {
        //     modeLabel.text = scavGraphics.scavenger.animation?.id?.value ?? "null";
        // }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            // modeLabel.RemoveFromContainer();
            base.InitiateSprites(sLeaser, rCam);
            sLeaser.sprites[firstSprite] = TriangleMesh.MakeLongMesh(parts, pointyTip: false, customColor: false);
            // rCam.ReturnFContainer("HUD2").AddChild(modeLabel);
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
        {
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);


            var triMesh = (TriangleMesh)sLeaser.sprites[firstSprite];
            if (parts >= 2)
            {
                triMesh.isVisible = true;
                Vector2 a = Vector2.Lerp(scavGraphics.hands[pendulumIndex].lastPos, scavGraphics.hands[pendulumIndex].pos, timeStacker);
                Vector2 b = Vector2.Lerp(pendulum.AbsoluteLastPosition, pendulum.AbsolutePosition, timeStacker);
                Vector2 perpAB = Vector2.Perpendicular(Custom.DirVec(a, b));

                Vector2 currentPartPos(int index)
                {
                    float t = (float)index / ((float)parts - 1);
                    Vector2 baseInterp = Vector2.Lerp(a, b, t);

                    if (pendulum.mode == ImperialPendulum.PendulumMode.Attached)
                    {
                        float x = Custom.Dist(baseInterp, b) / 20f;
                        const float j = 40f;
                        const float c = 1.5f;
                        float k = 1f - Math.Min(1.0f, pendulum.ticksSinceModeChange / 10f);
                        // float extralen = Mathf.Max(pendulum.attachedLength - (pendulum.ropeLength + 20f), 0f);
                        // k *= Math.Min(extralen / ImperialPendulum.MIN_ROPELENGTH, 1.0f);

                        // LaTex
                        // \frac{jk}{x\ +\ 1}\sin\left(\pi ck\ln\left(x\ +\ 1\right)\right)\left\{x\ >\ 0\right\}

                        float offset = (j * k) * t * Mathf.Sin(Mathf.PI * c * k * Mathf.Log(x + 1));

                        float lerptohandDist = 20f * 4f;
                        float disttohand = Custom.DistNoSqrt(baseInterp, a);
                        if (disttohand < (lerptohandDist * lerptohandDist))
                        {
                            Mathf.Lerp(offset, 0f, Mathf.Sqrt(disttohand) / lerptohandDist);
                        }
                        return baseInterp + perpAB * offset;
                    }

                    else if (pendulum.mode == ImperialPendulum.PendulumMode.Retracting)
                    {
                        float x = Custom.Dist(baseInterp, b) / 20f;
                        const float j = 40f;
                        const float c = 1.5f;
                        float k = Math.Min(1.0f, pendulum.ticksSinceModeChange / 10f);

                        // LaTex
                        // \frac{jk}{x\ +\ 1}\sin\left(\pi ck\ln\left(x\ +\ 1\right)\right)\left\{x\ >\ 0\right\}
            
                        float offset = (j * k) * t * Mathf.Sin(Mathf.PI * c * k * Mathf.Log(x + 1) + Time.time*10);

                        float lerptohandDist = 20f * 4f;
                        float disttohand = Custom.DistNoSqrt(baseInterp, a);
                        if (disttohand < (lerptohandDist * lerptohandDist))
                        {
                            Mathf.Lerp(offset, 0f, Mathf.Sqrt(disttohand) / lerptohandDist);
                        }
                        return baseInterp + perpAB * offset;
                    }

                    return baseInterp;
                }

                triMesh.SetPosition(Vector2.zero);
                for (int i = 0; i < parts; i++)
                {

                    int partIndex = i * 4;
                    Vector2 partPos = currentPartPos(i);
                    if (i == 0)
                    {
                        Vector2 followingPartPos = currentPartPos(i + 1);
                        Vector2 normalized = Custom.DirVec(partPos, followingPartPos);
                        Vector2 perpendicularVec = Vector2.Perpendicular(normalized);
                        triMesh.MoveVertice(partIndex + 0, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 1, (partPos + perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 2, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 3, (partPos + perpendicularVec * thickness) - camPos);
                    }
                    else
                    {
                        Vector2 formerPartPos = currentPartPos(Math.Max(i - 1, 0));
                        Vector2 normalized = Custom.DirVec(formerPartPos, partPos);
                        Vector2 perpendicularVec = Vector2.Perpendicular(normalized);
                        triMesh.MoveVertice(partIndex + 0, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 1, (partPos + perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 2, (partPos - perpendicularVec * thickness) - camPos);
                        triMesh.MoveVertice(partIndex + 3, (partPos + perpendicularVec * thickness) - camPos);
                    }
                }
                triMesh.isVisible = pendulum.mode != ImperialPendulum.PendulumMode.Rest;
            }
            else
            {
                triMesh.isVisible = false;
            }

            sLeaser.sprites[firstSprite].color = blackColor;


            // modeLabel.SetPosition(
            //     Vector2.Lerp(scavGraphics.scavenger.mainBodyChunk.lastPos, scavGraphics.scavenger.mainBodyChunk.pos, timeStacker) + new Vector2(0, 40f));
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            blackColor = palette.blackColor;
        }
    }

    public class ScavengerBearClaws
    {
        public static ConditionalWeakTable<Scavenger, ScavengerBearClaws> map = new();
        public static ScavengerBearClaws? GetClaws(Scavenger scav)
        {
            if (scav.isImperial())
            {
                return map.GetValue(scav, scavy => new ScavengerBearClaws(scavy));
            }
            return null;
        }

        public List<(PhysicalObject obj, int timeout)> delayedImmidieteGrab = new();
        public Scavenger scavenger;
        public bool[] wasmouseDown = new bool[3];
        public bool[] buffered = new bool[2];
        public ImperialPendulum[] pendulums;
        public ImperialPendulum? lastAttachedPendulum = null;
        public int lizardFlipDelay = 0;
        private ScavengerBearClaws(Scavenger scavenger)
        {
            this.scavenger = scavenger;

            pendulums = new ImperialPendulum[2];
            for (int i = 0; i < 2; i++)
            {
                pendulums[i] = new ImperialPendulum(scavenger.bodyChunks[0], i);
            }
        }

        public void ResetAllPendulums()
        {
            foreach (ImperialPendulum pendulum in pendulums)
            {
                pendulum.Reset();
            }
        }

        // // pathfinding stuff
        public SwingPathingAssistor.Node? dedicatedPath = null;
        public SwingPathingAssistor.Action? dedicatedAction = null;
        public int actionTimer = 0;
    }

    partial class ScavolutionPlugin
    {
        void PendulumHooks()
        {
            On.Scavenger.Update += ScavengerPendulum_ScavengerUpdate;
            On.Scavenger.Act += ScavengerPendulum_ScavengerAct;
            IL.ScavengerGraphics.ctor += ScavengerPendulum_ScavengerGraphics_ctor;
            On.ScavengerGraphics.ScavengerHand.Update += ScavengerPendulum_ScavengerGraphics_ScavengerHand_Update;
            On.Scavenger.CombatUpdate += ScavengerPendulum_CombatUpdate;
            On.Scavenger.TryThrow_BodyChunk_ViolenceType_Nullable1 += ScavengerImperial_Scavenger_TryThrow;

            // make sure pendulums are reset when moving rooms.
            On.Scavenger.NewRoom += ScavengerPendulum_Scavenger_NewRoom;
            On.Scavenger.SpitOutOfShortCut += ScavengerPendulum_Scavenger_SpitOutOfShortCut;
            On.UpdatableAndDeletable.RemoveFromRoom += ScavengerPendulum_UpdatableAndDeletable_RemoveFromRoom;
            On.UpdatableAndDeletable.Destroy += ScavengerPendulum_UpdatableAndDeletable_Destry;

            new Hook(typeof(ScavengerAI).GetProperty(nameof(ScavengerAI.HoldWeapon)).GetGetMethod(), ScavengerPendulum_ScavengerAI_HoldWeapon);
            On.Limb.FindGrip += ScavengerPendulum_FindGrip_Limb;
            On.Scavenger.TakeDownIncomingWeapon += ScavengerImperial_Scavenger_TakeDownIncomingWeapon;

            On.ScavengerAI.CheckHandsForSpear += ScavengerImperial_ScavengerAI_CheckHandsForSpear;
        }

        public bool ScavengerImperial_ScavengerAI_CheckHandsForSpear(On.ScavengerAI.orig_CheckHandsForSpear orig, global::ScavengerAI self)
        {
            if (self.scavenger.animation is ImperialPendulum.FlailAnimation) return true;
            return orig(self);
        }

        bool ScavengerImperial_Scavenger_TakeDownIncomingWeapon(On.Scavenger.orig_TakeDownIncomingWeapon orig, Scavenger self, Weapon weapon)
        {
            if (self.animation is not Scavenger.ThrowChargeAnimation && self.animation is not Scavenger.ThrowAnimation)
            {
                if (weapon is not ScavengerBomb && weapon is not ExplosiveSpear)
                {
                    if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws)
                    {
                        if (ScavengerImperial_GrabWithPendulum(self, weapon, false))
                        {
                            claws.delayedImmidieteGrab.Add((weapon, 3));
                            return true;
                        }
                    }
                }
            }
            
            return orig(self, weapon);
        }


        bool ScavengerPendulum_ScavengerAI_HoldWeapon(Func<ScavengerAI, bool> orig, ScavengerAI self)
        {
            if (ScavengerBearClaws.GetClaws(self.scavenger) is ScavengerBearClaws claws &&
                claws.pendulums[0].mode != ImperialPendulum.PendulumMode.Rest)
            {
                return false;
            }

            return orig(self);

        }

        public void ScavengerPendulum_FindGrip_Limb(On.Limb.orig_FindGrip orig, Limb self, Room room, Vector2 attachedPos, Vector2 searchFromPos, float maximumRadiusFromAttachedPos, Vector2 goalPos, int forbiddenXDirs, int forbiddenYDirs, bool behindWalls)
        {
            if (self.owner is ScavengerGraphics graph)
            {
                if (graph.scavenger.movMode == SEScavengerMovementModes.Swinging)
                {
                    return;
                }
            }

            orig(self, room, attachedPos, searchFromPos, maximumRadiusFromAttachedPos, goalPos, forbiddenXDirs, forbiddenYDirs, behindWalls);
        }

        public void ScavengerPendulum_Scavenger_NewRoom(On.Scavenger.orig_NewRoom orig, Scavenger self, Room newRoom)
        {
            try
            {
                if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws) claws.ResetAllPendulums();
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self, newRoom);
        }

        public void ScavengerPendulum_Scavenger_SpitOutOfShortCut(On.Scavenger.orig_SpitOutOfShortCut orig, Scavenger self, IntVector2 pos, Room newRoom, bool spitOutAllSticks)
        {
            try
            {
                if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws) claws.ResetAllPendulums();
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self, pos, newRoom, spitOutAllSticks);
        }

        public void ScavengerPendulum_UpdatableAndDeletable_Destry(On.UpdatableAndDeletable.orig_Destroy orig, UpdatableAndDeletable self)
        {
            try
            {
                if (self is Scavenger scav)
                {
                    if (ScavengerBearClaws.GetClaws(scav) is ScavengerBearClaws claws) claws.ResetAllPendulums();
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self);
        }

        public void ScavengerPendulum_UpdatableAndDeletable_RemoveFromRoom(On.UpdatableAndDeletable.orig_RemoveFromRoom orig, UpdatableAndDeletable self)
        {
            try
            {
                if (self is Scavenger scav)
                {
                    if (ScavengerBearClaws.GetClaws(scav) is ScavengerBearClaws claws) claws.ResetAllPendulums();
                }

            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }

            orig(self);
        }


        void ScavengerPendulum_ScavengerGraphics_ctor(ILContext context)
        {
            try
            {
                ILCursor cursor = new(context);
                cursor.GotoNext(MoveType.After, x => x.MatchStloc(1));
                cursor.MoveBeforeLabels();
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.Emit(OpCodes.Ldloca, 1);
                cursor.EmitDelegate((ScavengerGraphics gfx, PhysicalObject ow, ref int currentSprite) =>
                {
                    if (ScavengerBearClaws.GetClaws((Scavenger)ow) is ScavengerBearClaws claws)
                    {
                        for (int i = 0; i < claws.pendulums.Length; i++)
                        {
                            var pendulumgfx = new ImperialPendulumGraphics(claws.pendulums[i], i, gfx, currentSprite);
                            gfx.AddSubModule(pendulumgfx);
                            currentSprite += pendulumgfx.totalSprites;
                        }
                    }
                });

            }
            catch (Exception exception)
            {
                Logger.LogError(exception);
            }
        }


        
        void ScavengerPendulum_ScavengerUpdate(On.Scavenger.orig_Update orig, Scavenger self, bool eu)
        {
            if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws)
            {
                foreach (ImperialPendulum pendulum in claws.pendulums)
                {
                    pendulum.Update(eu);
                    if (!self.Consious && pendulum.IsAttached)
                    {
                        pendulum.Release();
                    }
                }

                if (self.movMode == SEScavengerMovementModes.Swinging && self.Consious)
                {
                    self.airFriction = 1.0f;
                }
                else
                {
                    self.airFriction = 0.999f;
                }

                if (!self.Consious)
                {
                    claws.delayedImmidieteGrab.Clear();
                }
            }


            orig(self, eu);
        }

        static bool ScavengerPendulum_OffGround(Scavenger scavenger)
        {
            if (scavenger.bodyChunks[1].contactPoint.y < 0)
            {
                return false;
            }

            if (scavenger.room.HasAnySolid(scavenger.room.GetTilePosition(scavenger.bodyChunks[1].pos) + new IntVector2(0, -1)))
            {
                return false;
            }

            bool isFloor(AItile.Accessibility accessibility)
            {
                return accessibility == AItile.Accessibility.Floor || accessibility == AItile.Accessibility.CurvedFloor || accessibility == AItile.Accessibility.Sand;
            }

            var tile = scavenger.room.GetTilePosition(scavenger.bodyChunks[1].pos);
            var acc = scavenger.room.aimap.getAItile(tile).acc;
            if ((acc == AItile.Accessibility.Climb && scavenger.movMode == Scavenger.MovementMode.Climb) || isFloor(acc))
            {
                return false;
            }

            if (scavenger.commitedToMove != default && !scavenger.CommitedToMoveIsDrop)
            {
                var startaitile = scavenger.room.aimap.getAItile(scavenger.commitedToMove.StartTile);
                var endaitile = scavenger.room.aimap.getAItile(scavenger.commitedToMove.DestTile);



                if (/* scavenger.commitedToMove.type != ImperialMovementConnection.SwingDetour && */
                    (isFloor(startaitile.acc) || isFloor(endaitile.acc)))
                {
                    return false;
                }
            }

            var aiTIle = scavenger.room.aimap.getAItile(scavenger.room.GetTilePosition(scavenger.mainBodyChunk.pos));
            if (aiTIle.narrowSpace || aiTIle.AnyWater) return false;
            return true;
        }

        bool ScavengerPendulum_ShouldKeepAttaching(Scavenger self, ImperialPendulum pendulum)
        {
            if (ControlledScavenger(self.abstractCreature)) return true;
            if (pendulum.stuckInObject is not null)
            {
                if (ImperialPendulum.getStuckPendulums(pendulum.stuckInObject).Count > 1 && pendulum.ticksSinceModeChange > 40f) return false;
                if (pendulum.stuckInObject is Creature critter)
                {
                    var rel = self.AI.DynamicRelationship(critter.abstractCreature);
                    // if (rel.GoForKill || rel.type == CreatureTemplate.Relationship.Type.Afraid) return true; // TODO: impelement slice attack.
                }
                if (pendulum.retractImmidietly) return true;
                return pendulum.ticksSinceModeChange < 2 * 40f;
            }

            return true;
        }


        public bool NotSlugcatPlayableGrabability(Scavenger scav, PhysicalObject obj)
        {
            if (SprobDesecratingGraves.ScavengerHooks.GetScavengerData(scav) is ObjectDataTypes.ScavengerData scavdata)
            {
                if (scavdata.controller != null)
                {
                    if (scavdata.controller.Grabability(obj) <= Player.ObjectGrabability.CantGrab)
                    {
                        return false;
                    } 
                }
            }

            return true;
        }

        [Obsolete]
        void ScavengerPendulum_ActionPath(ScavengerBearClaws claws, Scavenger self)
        {
            // follow pendulum path
            SwingPathingAssistor.Node? node = claws.dedicatedPath;
            if (node is null)
            {
                MovementConnection connection = self.FollowPath(self.room.ToWorldCoordinate(self.mainBodyChunk.pos), false);
                if (connection.type == ImperialMovementConnection.SwingDetour)
                {
                    var swingmodule = self.AI.modules.OfType<SwingPathingAssistor>().First();
                    node = swingmodule.FollowPath(connection);
                }

                if (node is not null)
                {
                    claws.dedicatedPath = node;
                }
                else
                {
                    claws.actionTimer = 0;
                    claws.dedicatedAction = null;
                }
            }

            if (node is not null && claws.dedicatedAction is null)
            {
                var nodes = new List<SwingPathingAssistor.Node>(SwingPathingAssistor.EnumerateNodes(node));
                nodes.Sort((a, b) =>
                {
                    float dista = Custom.DistNoSqrt(self.mainBodyChunk.pos, a.pos);
                    float distb = Custom.DistNoSqrt(self.mainBodyChunk.pos, b.pos);
                    if (dista < distb) return -1;
                    if (dista > distb) return 1;
                    return 0;
                });

                var bestNode = nodes.ElementAtOrDefault(1) ?? nodes.First();
                claws.dedicatedAction = bestNode.fromAction;
                if (claws.dedicatedAction.HasValue)
                {
                    claws.actionTimer = claws.dedicatedAction.Value.minActionConitinuity * SwingPathingAssistor.latticeTimeResolution;
                }
            }


            claws.actionTimer--;
            if (claws.actionTimer <= 0) claws.dedicatedAction = null;
            if (claws.dedicatedAction.HasValue)
            {
                if (claws.dedicatedAction.Value.swingAnchor.HasValue)
                {
                    var alreadyStuckToWall = claws.pendulums.Where(x => x.IsAttached && x.stuckInObject is null).ToArray();
                    if (!alreadyStuckToWall.Any(x => Custom.DistLess(x.absoluteAttachedPosition, claws.dedicatedAction.Value.swingAnchor.Value, 5f)))
                    {
                        ImperialPendulum bestpendulum = claws.pendulums.Except(alreadyStuckToWall).Where(x => x != claws.lastAttachedPendulum).FirstOrDefault() ?? claws.pendulums.First();
                        if (!bestpendulum.IsAttached || bestpendulum.stuckInObject is null)
                        {
                            foreach (ImperialPendulum pendulum in alreadyStuckToWall)
                            {
                                pendulum.Release();
                            }

                            bestpendulum.Send(claws.dedicatedAction.Value.swingAnchor.Value - bestpendulum.RestPosition);
                        }
                    }
                }
                else
                {
                    foreach (ImperialPendulum pendulum in claws.pendulums)
                    {
                        if (pendulum.IsAttached && pendulum.stuckInObject is null)
                        {
                            pendulum.Release();
                        }
                    }
                }
            }
            else
            {
                foreach (ImperialPendulum pendulum in claws.pendulums)
                {
                    if (pendulum.IsAttached && pendulum.stuckInObject is null)
                    {
                        pendulum.Release();
                    }
                }
            }
        }
        
        // void SwingFollowPath(Scavenger self, MovementConnection connection)
        // {
        //     MovementConnection movementConnection2 = connection;
        //     List<MovementConnection> connections = [];
        //     for (int m = 0; m < 5; m++)
        //     {
        //         self.connections.Add(movementConnection2);
        //         movementConnection2 = self.FollowPath(movementConnection2.destinationCoord, actuallyFollowingThisPath: false);
        //         for (int n = 0; n < self.connections.Count; n++)
        //         {
        //             if (!(movementConnection2 != default(MovementConnection)))
        //             {
        //                 break;
        //             }

        //             if (self.connections[n].destinationCoord == movementConnection2.destinationCoord)
        //             {
        //                 movementConnection2 = default(MovementConnection);
        //             }
        //         }

        //         if (movementConnection2 == default(MovementConnection))
        //         {
        //             break;
        //         }
        //     }
                
        //     for (int num7 = 0; num7 < 2; num7++)
        //     {
        //         Vector2 vector2 = Custom.DirVec(self.room.MiddleOfTile(connection.startCoord), self.room.MiddleOfTile(connection.destinationCoord));
        //         bool flag4 = false;
        //         if (num7 == 0)
        //         {
        //             int num8 = connections.Count - 1;
        //             while (num8 >= 0 && !flag4)
        //             {
        //                 if (self.room.GetTilePosition(self.bodyChunks[num7].pos).FloatDist(connections[num8].DestTile + new IntVector2(0, 1)) < 5f && self.room.VisualContact(self.bodyChunks[num7].pos, self.room.MiddleOfTile(connections[num8].DestTile + new IntVector2(0, 1))))
        //                 {
        //                     vector2 = Vector2.ClampMagnitude(self.room.MiddleOfTile(connections[num8].DestTile + new IntVector2(0, 1)) - self.bodyChunks[num7].pos, 5f) / 5f;
        //                     flag4 = true;
        //                 }

        //                 num8--;
        //             }
        //         }

        //         int num9 = connections.Count - 1;
        //         while (num9 >= 0 && !flag4)
        //         {
        //             if (self.room.GetTilePosition(self.bodyChunks[num7].pos).FloatDist(connections[num9].DestTile) < 5f && self.room.VisualContact(self.bodyChunks[0].pos, self.room.MiddleOfTile(connections[num9].DestTile)) && self.room.VisualContact(self.bodyChunks[1].pos, self.room.MiddleOfTile(connections[num9].DestTile)))
        //             {
        //                 vector2 = Vector2.ClampMagnitude(self.room.MiddleOfTile(connections[num9].DestTile) - self.bodyChunks[num7].pos, 5f) / 5f;
        //                 flag4 = true;
        //             }

        //             num9--;
        //         }

        //         if (!self.GoThroughFloors)
        //         {
        //             self.GoThroughFloors = vector2.y < 0f;
        //         }

        //         if ((vector2.y < -0.35f && num7 == 0) || (vector2.y > 0.35f && num7 == 1))
        //         {
        //             vector2.y *= 0.5f;
        //         }

        //         self.bodyChunks[num7].vel.x += vector2.x * 0.8f;
        //     }

        // }

        
        void ScavengerPendulum_ScavengerAct(On.Scavenger.orig_Act orig, Scavenger self)
        {
            Vector2 pendulumAvgOffset = Vector2.zero;
            if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws)
            {
                // controlled stuff
                if (ControlledScavenger(self.abstractCreature))
                {
                    if (self.inputWithoutDiagonals.HasValue && self.inputWithoutDiagonals.Value.spec)
                    {
                        if (claws.pendulums.All(x => ScavengerImperial_HandFree(self, x.pendulumIndex)))
                        {
                            if (claws.pendulums.FirstOrDefault() is ImperialPendulum pendulum)
                            {
                                if (pendulum.flailTimer <= 0)
                                {
                                    pendulum.BeginFlail();
                                }
                            }
                        }
                    }


                    if (self.abstractCreature.world.game.cameras[0].room == self.room)
                    {
                        for (int i = 0; i < claws.wasmouseDown.Count() && self.Consious; i++)
                        {
                            var mouseDown = Input.GetMouseButton(i);
                            if (mouseDown != claws.wasmouseDown[i])
                            {
                                claws.wasmouseDown[i] = mouseDown;
                                if (mouseDown && (claws.pendulums[i].mode != ImperialPendulum.PendulumMode.Retracting || claws.pendulums[i].ticksSinceModeChange > 5))
                                {
                                    if (i < 2)
                                    {
                                        if (claws.pendulums[i].IsAttached)
                                        {
                                            claws.pendulums[i].Release();
                                        }
                                        else
                                        {
                                            claws.pendulums[i].Send(((Vector2)Futile.mousePosition + self.room.game.cameras[0].pos - claws.pendulums[0].RestPosition) * 1.2f, true, 0, self.inputWithoutDiagonals?.pckp ?? false, self.inputWithoutDiagonals?.pckp ?? false, true, 
                                                true);
                                        }
                                    }
                                    else if (i == 2)
                                    {
                                        for (int j = 0; j < 2; j++)
                                        {
                                            if (claws.pendulums[j].IsAttached)
                                            {
                                                claws.pendulums[j].Release();
                                            }
                                            else
                                            {
                                                claws.pendulums[i].Send(((Vector2)Futile.mousePosition + self.room.game.cameras[0].pos - claws.pendulums[0].RestPosition) * 1.2f, true, 0, self.inputWithoutDiagonals?.pckp ?? false, self.inputWithoutDiagonals?.pckp ?? false, true, 
                                                    true);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // var clawsAttachedToWall = claws.pendulums.Where(x => x.IsAttachedToWall).ToList();
                    // MovementConnection connection = ((StandardPather)self.AI.pathFinder).FollowPath(self.room.ToWorldCoordinate(self.mainBodyChunk.pos), false);
                    // var desttile = self.room.aimap.getAItile(connection.DestTile);

                    // if ((connection != default) && (desttile.acc == AItile.Accessibility.Air || desttile.acc == AItile.Accessibility.Wall) &&
                    //     !self.room.aimap.IsConnectionAllowedForCreature(connection, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Scavenger)) && !connection.IsDrop && ScavengerPendulum_OffGround(self))
                    // {
                    //     if (!clawsAttachedToWall.Any())
                    //     {
                    //         var sentpendulum = claws.pendulums.LastOrDefault(x => x.mode == ImperialPendulum.PendulumMode.Rest);
                    //         if (sentpendulum is not null)
                    //         {
                    //             sentpendulum.Send(Vector2.up * 20f * 50);
                    //             sentpendulum.ropeLength = Custom.Dist(sentpendulum.AbsolutePosition, sentpendulum.RestPosition);
                    //             clawsAttachedToWall.Add(sentpendulum);
                    //         }
                    //     }

                    //     var futurePath = connection;
                    //     for (int i = 0; i < 4; i++)
                    //     {
                    //         var next = ((StandardPather)self.AI.pathFinder).FollowPath(connection.destinationCoord, false);
                    //         if (next == default) continue;
                    //         next = futurePath;
                    //     }

                    //     foreach (ImperialPendulum pendulum in clawsAttachedToWall)
                    //     {
                    //         pendulum.ropeLength = Mathf.Lerp(pendulum.ropeLength, Custom.Dist(self.room.MiddleOfTile(futurePath.DestTile), pendulum.AbsolutePosition), 0.1f);
                    //     }
                    // }
                    // else
                    // {
                    //     foreach (ImperialPendulum pendulum in clawsAttachedToWall)
                    //     {
                    //         pendulum.Release();
                    //     }
                    // }

                }           
                
                     
                
                //
                for (int i = claws.delayedImmidieteGrab.Count - 1; i >= 0; i--)
                {
                    claws.delayedImmidieteGrab[i] = (claws.delayedImmidieteGrab[i].obj, claws.delayedImmidieteGrab[i].timeout - 1);
                    if (claws.delayedImmidieteGrab[i].timeout <= 0)
                    {
                        if (claws.delayedImmidieteGrab[i].obj.room == self.room && !(claws.delayedImmidieteGrab[i].obj.slatedForDeletetion))
                        {
                            ScavengerImperial_GrabWithPendulum(self, claws.delayedImmidieteGrab[i].obj);
                        }
                        claws.delayedImmidieteGrab.Remove(claws.delayedImmidieteGrab[i]);
                    }
                }



                int activependulumCount = 0;
                foreach (ImperialPendulum pendulum in claws.pendulums)
                {
                    if (pendulum.IsAttached)
                    {
                        if (!ScavengerPendulum_ShouldKeepAttaching(self, pendulum))
                        {
                            pendulum.Release();
                        }
                        else
                        {
                            pendulumAvgOffset += pendulum.AbsolutePosition - pendulum.RestPosition;
                            activependulumCount += 1;
                        }
                    }


                    if (pendulum.retractImmidietly)
                    {
                        if (Custom.DistLess(pendulum.RestPosition, pendulum.AbsolutePosition, 2f * 20f))
                        {
                            if (pendulum.stuckInObject is PhysicalObject obj)
                            {
                                bool canGrab = pendulum.stuckInAppendage is null;
                                if (ScavolutionPlugin.NotSlugcatPlayables && canGrab)
                                {
                                    canGrab = canGrab && NotSlugcatPlayableGrabability(self, pendulum.stuckInObject);
                                }

                                if (!ScavolutionPlugin.ControlledScavenger(self.abstractCreature))
                                {
                                    if (self.AI.CollectScore(obj, false) <= 0)
                                    {
                                        canGrab = false;
                                    }
                                }
                                
                                if (canGrab)
                                {
                                    self.PickUpAndPlaceInInventory(obj);
                                }
                            }    

                            pendulum.Reset();
                        }
                    }
                    

                    if (pendulum.stuckInObject is not null && self.grasps.OfType<Creature.Grasp>().Select(x => x.grabbed).Contains(pendulum.stuckInObject))
                    {
                        pendulum.Reset();
                    }
                }

                

                if ((activependulumCount > 0 || self.movMode == SEScavengerMovementModes.Swinging) && ScavengerPendulum_OffGround(self))
                {
                    self.moveModeChangeCounter = 5;
                    self.movMode = SEScavengerMovementModes.Swinging;
                }

                var minimumDropScore = self.grasps.Aggregate(int.MaxValue, (int dropScore, Creature.Grasp grasp) =>
                {
                    if (grasp is null) return 0;
                    return Mathf.Min(dropScore, self.AI.DropScore(grasp.grabbed, true));
                });

    
                if (self.AI.scavengeCandidate is ItemTracker.ItemRepresentation rep && rep.VisualContact
                    && ( self.AI.CollectScore(self.AI.scavengeCandidate.representedItem.realizedObject, true) > minimumDropScore )
                    && !Custom.DistLess(rep.representedItem.realizedObject.firstChunk.pos, self.mainBodyChunk.pos, 20f * 3f) && rep.age > 10
                    && (rep.representedItem.realizedObject is not Weapon weapon || weapon.mode != Weapon.Mode.Thrown))
                {
                    if (ScavengerImperial_GrabWithPendulum(self, rep.representedItem.realizedObject))
                    {
                        self.AI.scavengeCandidate = null;   
                    }                    
                }

                claws.lizardFlipDelay -= 1;
                claws.lizardFlipDelay = Math.Max(claws.lizardFlipDelay, 0);
            }

            if (self.movMode == SEScavengerMovementModes.Swinging)
            {  
                if (ControlledScavenger(self.abstractCreature))
                {
                    self.mainBodyChunk.vel.x += (self.inputWithDiagonals?.x ?? 0f) * 0.8f;
                }

                self.stuckCounter = 0;
                self.AI.Update();
                self.CombatUpdate();

                if (self.animation is not ImperialPendulum.FlailAnimation && 
                    self.animation is not Scavenger.ThrowAnimation &&
                    self.animation is not Scavenger.ThrowChargeAnimation)
                {
                    self.animation = null;
                }
                else
                {
                    if (!self.animation.Continue)
                    {
                        self.animation = null;
                    }
                    else
                    {
                        self.animation.Update();
                    }
                }


                self.knucklePos = null;
                self.swingPos = null;
                self.nextSwingPos = null;

                if (pendulumAvgOffset != Vector2.zero)
                {
                    self.lookPoint = Vector2.Lerp(self.lookPoint, self.bodyChunks[1].pos + Custom.rotateVectorDeg(pendulumAvgOffset.normalized, 90f) * 300f, 0.09f);
                    self.flip = Mathf.Lerp(self.flip, Mathf.Clamp(
                        -Math.Sign(pendulumAvgOffset.x) * Mathf.Pow(Math.Abs(pendulumAvgOffset.x / (20f * 5f)), 2.4f)
                        , -1f, 1f), 0.09f);
                }

                Vector2 idealHeadPos = self.mainBodyChunk.pos + self.HeadLookDir * self.bodyChunkConnections[1].distance * 0.6f;
                self.bodyChunks[2].pos = Vector2.Lerp(self.bodyChunks[2].pos, idealHeadPos, 0.25f);


                var tile = self.room.GetTilePosition(self.bodyChunks[1].pos);

                if (self.room.aimap.getAItile(tile).smoothedFloorAltitude < 2)
                {
                    self.WeightedPush(0, 1, new Vector2(0f, 1f), Custom.LerpMap(Vector2.Dot(
                        (self.bodyChunks[0].pos - self.bodyChunks[1].pos).normalized, new Vector2(0f, 1f)), -1f, 1f, 5.5f, 0.3f) * 1f);
                }


                --self.moveModeChangeCounter;
                if (self.moveModeChangeCounter < 0)
                {
                    self.movMode = Scavenger.MovementMode.StandStill;
                }

                // MovementConnection con = self.FollowPath(self.room.ToWorldCoordinate(self.mainBodyChunk.pos), true);
                // SwingFollowPath(self, con);

                return;
            }
            else
            {
                orig(self);
            }
        }
        
        bool ScavengerImperial_HandFree(Scavenger scav, int hand, bool ignoreNonPendulumStuff = false)
        {
            if (ScavengerBearClaws.GetClaws(scav) is ScavengerBearClaws claws)
            {
                if (claws.pendulums[hand].mode != ImperialPendulum.PendulumMode.Rest) return false;
            }

            if (!ignoreNonPendulumStuff)
            {
                if (hand == 0)
                {
                    if (scav.animation is Scavenger.ThrowAnimation) return false;
                    if (scav.animation is Scavenger.ThrowChargeAnimation) return false;
                }
                
                if (scav.animation is Scavenger.PointingAnimation panim && panim.PointingArm == hand) return false;
                if (scav.animation is Scavenger.CommunicationAnimation canim && canim.GestureArm == hand) return false;
            }

            return true;
        }

        bool ScavengerImperial_GrabWithPendulum(Scavenger scav, PhysicalObject obj, bool actuallyGrab = true)
        {
            if (scav.room != obj.room || !scav.room.updateList.Contains(obj)) return false;
            if (obj is MoreSlugcats.SingularityBomb bomb && bomb.ignited) return false;
            if (obj is ScavengerBomb bomb2 && bomb2.ignited) return false;
            if (obj is FirecrackerPlant bomb3 && bomb3.fuseCounter > 0) return false;

            Logger.LogDebug("attempting to grab " + obj.abstractPhysicalObject.ToString());
            if (ScavengerBearClaws.GetClaws(scav) is ScavengerBearClaws claws)
            {
                if (scav.animation is ImperialPendulum.FlailAnimation) return false;
                if (claws.pendulums.LastOrDefault(x => ScavengerImperial_HandFree(scav, x.pendulumIndex)) is ImperialPendulum grabpendulum)
                {
                    if (actuallyGrab)
                    {
                        if (obj is Creature critter)
                        {
                            critter.enteringShortCut = null;
                        }
                        scav.AI.itemTracker.RepresentationForObject(obj, false)?.Destroy();
                        var hit = new SharedPhysics.CollisionResult(obj, obj.firstChunk, null, true, obj.firstChunk.pos);
                        scav.room.PlaySound(SoundID.Slugcat_Throw_Rock, new ImperialPendulum.PendulumSoundEmitter(grabpendulum, 1.0f, 1.0f), false, 1.0f, 1.0f, false);
                        grabpendulum.HitSomething(hit, obj.firstChunk.pos - grabpendulum.RestPosition, Vector2.zero, 0f, true);
                        grabpendulum.priorityPull = Mathf.Max(grabpendulum.priorityPull, 0.7f);
                        grabpendulum.ropeLength = 0;
                        grabpendulum.retractImmidietly = true;
                    }
                    return true;
                }
            }
            return false;
        }

        void ScavengerImperial_Scavenger_TryThrow(On.Scavenger.orig_TryThrow_BodyChunk_ViolenceType_Nullable1 orig, Scavenger self, BodyChunk aimChunk, ScavengerAI.ViolenceType violenceType, Vector2? aimPosition)
        {

            if (self.animation is ImperialPendulum.FlailAnimation flail)
            {
                float num = (self.room.game.IsStorySession ? (self.AI.agitation * 0.25f) : Mathf.InverseLerp(0f, 1f - Mathf.Pow(self.reactionSkill, 3f), self.AI.agitation));
                if (self.AI.agitation == 1f && !ModManager.DLCShared)
                {
                    self.reflexBuildUp = Mathf.Clamp(self.reflexBuildUp + Mathf.Lerp(1f / 120f, 0.1f, self.reactionSkill), num, 1f);
                }
                else if (self.AI.agitation != 1f)
                {
                    self.reflexBuildUp = Mathf.Max(num, self.reflexBuildUp - Mathf.Lerp(1f / 30f, 0.002f, self.reactionSkill));
                    self.fastReflexBuildUp = Mathf.Max(num, self.fastReflexBuildUp - Mathf.Lerp(1f / 30f, 0.002f, self.reactionSkill));
                }

                // Too OP. scav will instantly kills anything it sees
                // if (self.ReactionCheck() && self.FastReactionCheck() && (UnityEngine.Random.value < 0.02f))
                // {
                //     flail.ThrowCheck();
                // }
            }
            else
            {
                if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws && claws.pendulums[0].mode != ImperialPendulum.PendulumMode.Rest)
                {
                    return;
                }

                orig(self, aimChunk, violenceType, aimPosition);
            }
        }
        
        void ScavengerPendulum_CombatUpdate(On.Scavenger.orig_CombatUpdate orig, Scavenger self)
        {
            if ((self.AI.preyTracker.MostAttractivePrey ?? self.AI.threatTracker.mostThreateningCreature) is Tracker.CreatureRepresentation rep && self.AI.currentViolenceType == ScavengerAI.ViolenceType.Lethal)
            {
                var shouldAttackWithFlail = (
                    rep.representedCreature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.Vulture ||
                    rep.representedCreature.creatureTemplate.TopAncestor().type == CreatureTemplate.Type.MirosBird ||
                    rep.representedCreature.creatureTemplate.type == CreatureTemplate.Type.RedLizard ||
                    rep.representedCreature.creatureTemplate.type == CreatureTemplate.Type.RedCentipede ||
                    (ModManager.MSC && rep.representedCreature.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.TrainLizard) ||
                    (ModManager.Watcher && rep.representedCreature.creatureTemplate.type == Watcher.WatcherEnums.CreatureTemplateType.BlizzardLizard) ||
                    (ModManager.Watcher && rep.representedCreature.creatureTemplate.TopAncestor().type == Watcher.WatcherEnums.CreatureTemplateType.Loach)
                    ) || ((self.grasps.OfType<Creature.Grasp>().Select(x => self.AI.WeaponScore(x.grabbed, false, false)).Sum() <= 0) && rep.age > 40f * 6f);
                
                if (rep.representedCreature.realizedCreature?.room == self.room)
                {
                    var inAccessableTile = self.room.aimap.TileAccessibleToCreature(self.room.GetTilePosition(rep.representedCreature.realizedCreature.firstChunk.pos), rep.representedCreature.creatureTemplate);
                    shouldAttackWithFlail = shouldAttackWithFlail || (Math.Abs(Custom.DirVec(self.mainBodyChunk.pos, rep.representedCreature.realizedCreature.firstChunk.pos).y) > 0.25f && (inAccessableTile || rep.age > 40f * 3f));
                }

                shouldAttackWithFlail = shouldAttackWithFlail && rep.VisualContact;

                if (shouldAttackWithFlail)
                {
                    if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws)
                    {
                        if (claws.pendulums.All(x => ScavengerImperial_HandFree(self, x.pendulumIndex)))
                        {
                            if (claws.pendulums.FirstOrDefault() is ImperialPendulum pendulum)
                            {
                                if (pendulum.flailTimer <= 0)
                                {
                                    pendulum.BeginFlail();
                                }
                            }
                        }
                    }
                }
            }


            if (self.animation is ImperialPendulum.FlailAnimation flail)
            {
                if (self.immediatelyThrowAtChunk is not null)
                {
                    flail.ThrowCheck();
                }
                else if (self.AI.focusCreature is Tracker.CreatureRepresentation attackingrep && attackingrep.VisualContact && 
                    attackingrep.representedCreature?.realizedCreature is Creature critter && 
                    Custom.DistLess(critter.mainBodyChunk.pos, self.mainBodyChunk.pos, 40 + critter.mainBodyChunk.rad + self.mainBodyChunk.rad))
                {
                    flail.ThrowCheck();
                }
            }
            else
            {
                if (self.AI.focusCreature is Tracker.CreatureRepresentation lizrep &&
                    lizrep.representedCreature.realizedCreature is Lizard liz &&
                    self.AI.focusCreature.VisualContact)
                {
                    if (Mathf.Sign(self.mainBodyChunk.pos.x - liz.mainBodyChunk.pos.x) == Mathf.Sign(liz.bodyChunks[0].pos.x - liz.bodyChunks[1].pos.x) &&
                        liz.abstractCreature.creatureTemplate.type != CreatureTemplate.Type.RedLizard && (self.AI.behavior == ScavengerAI.Behavior.Attack || self.AI.behavior == ScavengerAI.Behavior.Flee))
                    {
                        ScavengerImperial_AttemptToRotateLizard(self, lizrep, liz);
                    }
                }

                if (ScavengerImperial_HandFree(self, 0, true))
                {
                    orig(self);
                }
                
            }
        }
        
        public void ScavengerImperial_AttemptToRotateLizard(Scavenger self, Tracker.CreatureRepresentation lizrep, Lizard liz)
        {
            if (ScavengerBearClaws.GetClaws(self) is ScavengerBearClaws claws && self.movMode != SEScavengerMovementModes.Swinging)
            {
                if (claws.lizardFlipDelay > 0) return;
                if (claws.pendulums.All(x => ScavengerImperial_HandFree(self, x.pendulumIndex)))
                {
                    if (claws.pendulums.LastOrDefault() is ImperialPendulum pendulum)
                    {
                        bool friendinWay = false;
                        BodyChunk bodyChunk = liz.bodyChunks[0];
                        for (int j = 0; j < self.AI.tracker.CreaturesCount; j++)
                        {
                            if (self.AI.tracker.GetRep(j).dynamicRelationship.currentRelationship.type == CreatureTemplate.Relationship.Type.Pack &&
                                self.AI.tracker.GetRep(j).representedCreature.realizedCreature != null &&
                                !self.AI.tracker.GetRep(j).representedCreature.realizedCreature.dead &&
                                self.AI.tracker.GetRep(j).representedCreature.realizedCreature.room == self.room &&
                                Custom.DistLess(self.AI.tracker.GetRep(j).representedCreature.realizedCreature.mainBodyChunk.pos,
                                    Custom.ClosestPointOnLineSegment(pendulum.RestPosition,
                                    bodyChunk.pos, self.AI.tracker.GetRep(j).representedCreature.realizedCreature.mainBodyChunk.pos), 40f))
                            {
                                friendinWay = true;
                                break;
                            }
                        }

                        if (!friendinWay)
                        {
                            pendulum.Send(bodyChunk.pos - pendulum.RestPosition, true, 0, false, false, false);
                            claws.lizardFlipDelay = 100;
                        }
                    }
                }
            }


            
        }

        void ScavengerPendulum_ScavengerGraphics_ScavengerHand_Update(On.ScavengerGraphics.ScavengerHand.orig_Update orig, ScavengerGraphics.ScavengerHand self)
        {
            try
            {
                orig(self);
                if (ScavengerBearClaws.GetClaws(self.scavenger) is ScavengerBearClaws pendulums)
                {
                    if (pendulums.pendulums[self.limbNumber].mode == ImperialPendulum.PendulumMode.Flail)
                    {
                        Vector2 ourshoulderpos = (Vector2)ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(self);
                        Vector2 relativePendulumOrigin = pendulums.pendulums[self.limbNumber].RestPosition - ourshoulderpos;
                        relativePendulumOrigin = Vector2.MoveTowards(relativePendulumOrigin, pendulums.pendulums[self.limbNumber].AbsolutePosition, 0.5f);

                        self.pos = Vector2.MoveTowards(ourshoulderpos, relativePendulumOrigin + ourshoulderpos, self.armLength);
                    }
                    if (pendulums.pendulums[self.limbNumber].mode != ImperialPendulum.PendulumMode.Rest)
                    {
                        Vector2 ourshoulderpos = (Vector2)ScavengerJunior_ScavengerGraphics_ScavengerHand_ShoulderJoint(self);
                        Vector2 pendulumpos = pendulums.pendulums[self.limbNumber].AbsolutePosition;
                        self.pos = Vector2.MoveTowards(ourshoulderpos, pendulumpos, self.armLength);
                        self.vel = Vector2.zero;
                        self.reachedSnapPosition = true;
                    }
                }
            }
            catch (Exception except)
            {
                Logger.LogError(except);
            }
        }
    }
}
