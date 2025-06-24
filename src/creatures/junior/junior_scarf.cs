
using System;
using RWCustom;
using Unity.Mathematics;
using UnityEngine;

namespace Scavolution
{
    class JuniorScarf : ScavengerCosmetic.Template
    {
        public JuniorScarf(ScavengerGraphics owner, int firstSprite)
        : base(owner, firstSprite)
        {
            totalSprites = 2;
            segments = new SimpleSegment[size, size];
        }

        float length = 60f;
        float width = 7f;
        const int size = 6;
        private SimpleSegment[,] segments;

        public Vector2 NeckPos(float timeStacker) {
            Vector2 headpos = Vector2.Lerp(this.scavGrphs.scavenger.bodyChunks[2].lastPos, this.scavGrphs.scavenger.bodyChunks[2].pos, timeStacker);
            Vector2 bodypos = Vector2.Lerp(this.scavGrphs.scavenger.bodyChunks[0].lastPos, this.scavGrphs.scavenger.bodyChunks[0].pos, timeStacker);
            return Vector2.Lerp(headpos, bodypos, 0.4f);
        }
        public override void Update()
        {
            float segmentlength = length / size;
            float segmentwidth = width / size;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    ref SimpleSegment segment = ref segments[i, j];
                    segment.lastPos = segment.pos;
                    segment.vel = Vector2.ClampMagnitude(segment.vel * 0.95f, 10f);
                    segment.vel.y -= 0.4f * scavGrphs.scavenger.room.gravity;
                    if (i > 0)
                    {
                        this.ConnectSegments(ref segment, ref segments[i - 1, j], segmentlength, 0.7f);
                    }
                    if (j > 0)
                    {
                        this.ConnectSegments(ref segment, ref segments[i, j - 1], segmentwidth, 0.9f);
                    }
                }
            }


            for (int i = 0; i < size; i++)
            {
                float segment_pos = ((float)(i / (size - 1)) - 0.5f);
                segment_pos *= width;

                BodyChunk mainBodyChunk = base.scavGrphs.scavenger.mainBodyChunk;
                ref SimpleSegment segment = ref segments[i, 0];
                Vector2 neck = NeckPos(1f);
                segment.pos.x = neck.x;
                segment.pos.y = neck.y + segment_pos;
            }
            Room room = scavGrphs.scavenger.room;
            for (int i = 0; i < size; i++)
            {
                float segment_pos = ((float)(i / (size - 1)) - 0.5f);
                for (int j = 1; j < size; j++)
                {
                    float segment_distance = (float)(j / (size - 1));

                    ref SimpleSegment segment = ref segments[i, j];
                    ref SimpleSegment back_segment = ref segments[i, j - 1];
                    Vector2 dir = (segment.pos - back_segment.pos).normalized;
                    if (dir.x < 0.05f)
                    {
                        dir.x = Mathf.Sign(scavGrphs.flip) * 0.05f;

                        // simplification of sin(acos(x)) 
                        dir.y = Mathf.Sqrt(1 - (dir.x * dir.x)) * Math.Sign(dir.y);
                    }
                    
                    segment.pos += segment.vel;
                    if (room.GetTile(segment.lastPos).Solid && Custom.DistLess(segment.lastPos, segment.pos, segmentlength * 4f))
                    {
                        float rad = Mathf.Lerp(3f, 1f, (float)j / ((float)size - 1f));
                        SharedPhysics.TerrainCollisionData terrainCollisionData = new SharedPhysics.TerrainCollisionData(segment.pos, segment.lastPos, segment.vel, rad, default(IntVector2), true);
                        terrainCollisionData = SharedPhysics.HorizontalCollision(room, terrainCollisionData);
                        terrainCollisionData = SharedPhysics.VerticalCollision(room, terrainCollisionData);
                        segment.pos = terrainCollisionData.pos;
                        segment.vel = terrainCollisionData.vel;
                    }
                }
            }
        }

        public override void Reset()
        {
            for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					this.segments[j, i].Reset(this.owner.owner.bodyChunks[0].pos);
				}
			}
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites[firstSprite] = TriangleMesh.MakeGridMesh("Futile_White", size - 1); // scarf dangle
            sLeaser.sprites[firstSprite + 1] = TriangleMesh.MakeGridMesh("Futile_White", 1); // neck
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            TriangleMesh dangle = (TriangleMesh)sLeaser.sprites[firstSprite];
            TriangleMesh neck = (TriangleMesh)sLeaser.sprites[firstSprite + 1];

            int dangle_index = 0;
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    dangle.MoveVertice(dangle_index++, segments[i, j].DrawPos(timeStacker) - camPos);
                }
            }

            int neck_index = 0;
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {

                    float segment_height = (i - 1f) * 3f;
                    float segment_width = (j - 1f) * 8f;
                    Vector2 posrelativetohead = new float2(segment_width, segment_height);
                    posrelativetohead = Custom.rotateVectorDeg(posrelativetohead, Custom.VecToDeg(scavGrphs.HeadDir(timeStacker)));
                    Vector2 neckpos = NeckPos(1f);
                    neck.MoveVertice(neck_index++, posrelativetohead + neckpos - camPos);
                }
            }


            neck.color = new Color(255, 165, 0);
            dangle.color = new Color(255, 165, 0);
            
            
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {

        }

        public void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer container)
        {
            rCam.ReturnFContainer("Bloom").AddChild(sLeaser.sprites[firstSprite]);
        }

		private void ConnectSegments(ref SimpleSegment A, ref SimpleSegment B, float targetDist, float massRatio)
		{
			Vector2 difference = B.pos - A.pos;
			float magnitude = difference.magnitude;
			if (magnitude > targetDist)
			{
				Vector2 a2 = difference / magnitude;
				float error = targetDist - magnitude;
				A.pos -= 0.45f * error * a2 * massRatio;
				A.vel -= 0.15f * error * a2 * massRatio;
				B.pos += 0.45f * error * a2 * (1f - massRatio);
				B.vel += 0.15f * error * a2 * (1f - massRatio);
			}
		}
    }


}