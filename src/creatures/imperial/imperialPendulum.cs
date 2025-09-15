using System;
using RWCustom;
using LBMergedMods.Hooks;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Linq;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace Scavolution
{
    public class ImperialPendulum : SharedPhysics.IProjectileTracer
    {
        public class PendulumMode : ExtEnum<PendulumMode>
        {
            public static readonly PendulumMode Rest = new PendulumMode(nameof(Rest), true);
            public static readonly PendulumMode Retracting = new PendulumMode(nameof(Retracting), true);
            public static readonly PendulumMode Attached = new PendulumMode(nameof(Attached), true);

            public PendulumMode(string value, bool register = false)
                : base(value, register)
            {

            }
        }

        public PendulumMode mode = PendulumMode.Rest;


        public Vector2 rotation;
        public Vector2 absoluteAttachedPosition;


        public Vector2 retractingVelocity;
        public Vector2 retractingAbsolutelastPosition;
        public Vector2 retractingRelativePosition;
        
        public Vector2 AbsoluteLastPosition
        {
            get
            {
                if (mode == PendulumMode.Attached)
                {
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

                return connectedChunk.pos;
            }
        }

        public Vector2 AbsolutePosition
        {
            get
            {
                if (mode == PendulumMode.Attached)
                {
                    if (this.stuckInObject is not null)
                    {
                        var chunk = this.stuckInObject.bodyChunks[this.stuckInChunkIndex];
                        return chunk.pos + -rotation.normalized * chunk.rad;
                    }

                    return absoluteAttachedPosition;
                }

                if (mode == PendulumMode.Retracting)
                {
                    return connectedChunk.pos + retractingRelativePosition;
                }

                return connectedChunk.pos;
            }
        }




        public int ticksSinceModeChange = 0;
        public PhysicalObject? stuckInObject;
        public int stuckInChunkIndex;


        public BodyChunk connectedChunk;
        public PhysicalObject owner => connectedChunk.owner;
        public bool IsActive => mode == PendulumMode.Attached;
        public float ropeLength = 7 * 20f;
        public float shootRopeLength = 18 * 20f;

        public const int ticksForFullElasticity = 20;
        public const int ticksToRetract = 60;



        public ImperialPendulum(BodyChunk chunk)
        {
            connectedChunk = chunk;
            Reset();
        }

        public void Reset()
        {
            mode = PendulumMode.Rest;
            ticksSinceModeChange = 0;
            stuckInObject = null!;
            stuckInChunkIndex = -1;
        }

        public void Release()
        {
            retractingRelativePosition = AbsolutePosition - connectedChunk.pos;
            retractingAbsolutelastPosition = AbsolutePosition;
            mode = PendulumMode.Retracting;
            ticksSinceModeChange = 0;
            stuckInObject = null!;
            stuckInChunkIndex = -1;
        }


        public void Update(bool eu)
        {
            ticksSinceModeChange++;
            if (mode == PendulumMode.Attached)
            {
                if (stuckInObject != null && (stuckInObject.slatedForDeletetion || owner.room != stuckInObject.room))
                {
                    Release();
                }


                Elasticity();
            }

            if (mode == PendulumMode.Retracting)
            {
                retractingVelocity = Vector2.Lerp(retractingVelocity, AbsolutePosition - connectedChunk.pos, (ticksSinceModeChange / ticksToRetract));
                retractingRelativePosition = Vector2.Lerp(retractingRelativePosition, Vector2.zero, 0.1f*(ticksSinceModeChange / ticksToRetract));
                retractingRelativePosition += retractingVelocity;
                SharedPhysics.CollisionResult result = SharedPhysics.CollisionResultTraceTerrainCollision(owner.room,
                    AbsolutePosition, AbsoluteLastPosition, 5f, true
                );

                retractingRelativePosition = result.collisionPoint - connectedChunk.pos;
                if (retractingRelativePosition.magnitude < 5f)
                {
                    Reset();
                }
            }
        }

        public bool HitThisObject(PhysicalObject obj)
        {
            if (obj == owner) return false;
            return obj is Creature && obj.collisionLayer == owner.collisionLayer && obj.abstractPhysicalObject.SameRippleLayer(owner.abstractPhysicalObject) && obj.CollideWithObjects && obj.canBeHitByWeapons;
        }

        public bool HitThisChunk(BodyChunk chunk)
        {
            return true;
        }

        public void Send(Vector2 direction, bool hitBodyChunks)
        {
            Reset();
            priorityPull = 0;
            ticksSinceModeChange = 0;
            SharedPhysics.CollisionResult result = SharedPhysics.CollisionResultTraceTerrainCollision(owner.room,
                AbsolutePosition + direction * shootRopeLength, AbsolutePosition, 5f, true
            );

            if (hitBodyChunks)
            {
                Vector2 collisionPoint = result.collisionPoint;
                result = SharedPhysics.TraceProjectileAgainstBodyChunks(this, owner.room, AbsolutePosition, ref collisionPoint, 5f, owner.collisionLayer, owner, false);
            }

            if (result.hitSomething)
            {
                mode = PendulumMode.Attached;
                if (result.chunk is null)
                {
                    absoluteAttachedPosition = result.collisionPoint;
                }
                else
                {
                    stuckInObject = result.obj;
                    stuckInChunkIndex = result.chunk.index;

                    var chunk = stuckInObject.bodyChunks[stuckInChunkIndex];
                    Vector2 forceDirection = (chunk.pos - connectedChunk.pos).normalized;

                    chunk.pos += forceDirection * 2f / chunk.mass;

                    if (stuckInObject is Creature critter)
                    {
                        if (owner is Creature mycritter) critter.SetKillTag(mycritter.abstractCreature);
                        if (critter is Lizard lizard)
                        {
                            if (chunk.index == 0 && lizard.HitHeadShield(direction))
                            {
                                owner.room.AddObject(new Spark(lizard.firstChunk.pos, Custom.RNV() * 60f * UnityEngine.Random.value, Color.white, null, 20, 50));
                                Release();
                                critter.Violence(null, forceDirection * 3f, chunk, null, Creature.DamageType.Blunt, 0.01f, 60);
                                lizard.turnedByRockDirection = (int)Mathf.Sign(direction.x);
                                lizard.turnedByRockCounter = 20;
                                return;
                            }
                            else if (chunk.index == 0 && lizard.HitInMouth(direction))
                            {
                                priorityPull = 0.25f;
                                critter.Violence(null, forceDirection * 3f, chunk, null, Creature.DamageType.Stab, 0.2f, 120);
                                return;
                            }
                        }

                        critter.Violence(null, forceDirection * 3f, chunk, null, Creature.DamageType.Stab, 0.1f, 20);
                    }
                    else
                    {
                        chunk.vel += forceDirection * 3f;
                        return;
                    }
                }
            }
            else
            {
                Release();
                retractingRelativePosition = AbsolutePosition + direction * shootRopeLength;
                retractingAbsolutelastPosition = AbsolutePosition + direction * shootRopeLength;
            }            
        }


        public float priorityPull = 0f;
        public void Elasticity()
        {
            if (this.stuckInObject is not null)
            {
                var chunk = this.stuckInObject.bodyChunks[this.stuckInChunkIndex];
            }

            var difference = AbsolutePosition - connectedChunk.pos;
            
            
            if (difference.sqrMagnitude > ropeLength * ropeLength)
            {
                float magnitudeMass = 1.0f;
                if (stuckInObject is not null)
                {
                    magnitudeMass = stuckInObject.TotalMass / (stuckInObject.TotalMass + owner.TotalMass);
                    if (priorityPull > 0f) magnitudeMass = Mathf.Lerp(magnitudeMass, 0f, priorityPull);
                    if (priorityPull < 0f) magnitudeMass = Mathf.Lerp(magnitudeMass, 1f, -priorityPull);
                }

                connectedChunk.pos = Vector2.Lerp(connectedChunk.pos, AbsolutePosition + (difference.normalized * ropeLength), 0.08f * magnitudeMass);
                connectedChunk.vel += -difference.normalized * (difference.magnitude - ropeLength) * 0.45f * magnitudeMass * Math.Min((float)ticksSinceModeChange / (float)ticksForFullElasticity, 4f);

                if (stuckInObject is not null)
                {
                    var stuckinChunk = stuckInObject.bodyChunks[stuckInChunkIndex];
                    stuckinChunk.pos = Vector2.Lerp(connectedChunk.pos, connectedChunk.pos - (difference.normalized * ropeLength), 0.08f * (1.0f - magnitudeMass));
                    stuckinChunk.vel -= -difference.normalized * (difference.magnitude - ropeLength) * 0.45f * (1.0f - magnitudeMass) * Math.Min((float)ticksSinceModeChange / (float)ticksForFullElasticity, 4f);
                }
            }
        }
    }

    public class ImperialPendulumGraphics : ComplexGraphicsModule.GraphicsSubModule
    {
        float width = 1.5f;
        int pendulumIndex;
        ImperialPendulum pendulum;
        ScavengerGraphics scavGraphics => (ScavengerGraphics)owner;
        public ImperialPendulumGraphics(ImperialPendulum pendulum, int pendulumIndex, ComplexGraphicsModule owner, int firstSprite) : base(owner, firstSprite)
        {
            this.totalSprites = 1;
            this.pendulumIndex = pendulumIndex;
            this.pendulum = pendulum;
        }

        public override void Update()
        {
            
        }

        public override void Reset()
        {

        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            base.InitiateSprites(sLeaser, rCam);
            sLeaser.sprites[firstSprite] = new FSprite("pixel");
            sLeaser.sprites[firstSprite].anchorY = 0f;
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, UnityEngine.Vector2 camPos)
        {
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
            Vector2 a = Vector2.Lerp(scavGraphics.hands[pendulumIndex].lastPos, scavGraphics.hands[pendulumIndex].pos, timeStacker);
            Vector2 b = Vector2.Lerp(pendulum.AbsoluteLastPosition, pendulum.AbsolutePosition, timeStacker);
            sLeaser.sprites[firstSprite].x = b.x - camPos.x;
            sLeaser.sprites[firstSprite].y = b.y - camPos.y;
            sLeaser.sprites[firstSprite].scaleY = Vector2.Distance(b, a);
            sLeaser.sprites[firstSprite].scaleX = width;
            sLeaser.sprites[firstSprite].rotation = Custom.AimFromOneVectorToAnother(b, a);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            sLeaser.sprites[firstSprite].color = palette.blackColor;
        }
    }


    partial class ScavolutionPlugin
    {
        public class ScavengerPendulums
        {
            public static ConditionalWeakTable<Scavenger, ScavengerPendulums> map = new();
            public static ScavengerPendulums? GetPendulums(Scavenger scav)
            {
                if (scav.isImperial())
                {
                    return map.GetValue(scav, scavvy => new ScavengerPendulums(scavvy));
                }


                return null;
            }

            public Scavenger scavenger;
            public ImperialPendulum[] pendulums;
            private ScavengerPendulums(Scavenger scavenger) {
                this.scavenger = scavenger;

                pendulums = new ImperialPendulum[2];
                for (int i = 0; i < 2; i++)
                {
                    pendulums[i] = new ImperialPendulum(scavenger.mainBodyChunk);
                }
            }
        }

        void PendulumHooks()
        {
            On.Scavenger.Update += ScavengerPendulum_ScavengerUpdate;
            On.Scavenger.Act += ScavengerPendulum_ScavengerAct;
            IL.ScavengerGraphics.ctor += ScavengerPendulum_ScavengerGraphics_ctor;
        }

        void ScavengerPendulum_ScavengerGraphics_ctor(ILContext context) {
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
                    if (ScavengerPendulums.GetPendulums((Scavenger)ow) is ScavengerPendulums pendulums)
                    {
                        for (int i = 0; i < pendulums.pendulums.Length; i++)
                        {
                            var pendulumgfx = new ImperialPendulumGraphics(pendulums.pendulums[i], i, gfx, currentSprite);
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
            if (ScavengerPendulums.GetPendulums(self) is ScavengerPendulums pendulums)
            {
                foreach (ImperialPendulum pendulum in pendulums.pendulums)
                {
                    pendulum.Update(eu);
                    if (!self.Consious && pendulum.IsActive) pendulum.Release();
                }
            }


            orig(self, eu);
        }

        void ScavengerPendulum_ScavengerAct(On.Scavenger.orig_Act orig, Scavenger self)
        {
            bool activePendulumsOrFalling = false;
            if (ScavengerPendulums.GetPendulums(self) is ScavengerPendulums pendulums)
            {
                activePendulumsOrFalling = pendulums.pendulums.Any(x => x.IsActive);
            }

            if (activePendulumsOrFalling)
            {
                self.AI.Update();
                if (self.animation is not null) self.animation = null;
                return;
            }
            else
            {
                orig(self);
            }
        }
    }
}
