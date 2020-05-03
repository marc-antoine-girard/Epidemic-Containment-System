﻿using Enums;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

[DisableAutoCreation]
[UpdateBefore(typeof(EnnemieFollowSystem))]
public class PathFollowSystem : SystemBase
{
    private static readonly CollisionFilter Filter = new CollisionFilter
    {
        BelongsTo = 1 << 2,
        CollidesWith = 1 << 10 | 1 << 1,
        GroupIndex = 0
    };

    private BuildPhysicsWorld buildPhysicsWorld;

    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    // static Unity.Mathematics.Random rSeed;
    protected override void OnCreate()
    {
        // rSeed = new Random(1235);
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        buildPhysicsWorld = World.GetOrCreateSystem<BuildPhysicsWorld>();
    }

    protected override void OnUpdate()
    {
        //rSeed = new Random(1235);
        ComponentDataContainer<PlayerTag> Player = new ComponentDataContainer<PlayerTag>
        {
            Components = GetComponentDataFromEntity<PlayerTag>()
        };
        var physicsWorld = buildPhysicsWorld.PhysicsWorld;
        double time = Time.ElapsedTime;
        float deltaTime = Time.DeltaTime;
        float test = 0.5f;
        float3 posPlayer = EntityManager.GetComponentData<Translation>(GameVariables.Player.Entity).Value;
        Entities.ForEach(delegate(int entityInQueryIndex, DynamicBuffer<PathPosition> pathPos, ref PathFollowComponent pathFollow, ref AttackRangeComponent range, in Translation translation)
        {
            range.IsInRange = false;
            switch (pathFollow.ennemyState)
            {
                case EnnemyState.Attack:
                    AttackFollow(ref pathFollow, posPlayer, translation, ref range);
                    break;
                case EnnemyState.Chase:
                    ChaseFollow(ref pathFollow, translation, posPlayer, ref physicsWorld, pathPos, ref Player);
                    break;
                case EnnemyState.Wondering:
                    WonderingFollow(ref pathFollow,ref physicsWorld, translation, time, entityInQueryIndex, deltaTime);
                    break;
            }

            //State changer 
            if (math.distance(translation.Value, posPlayer) <= 40)
            {
                RaycastInput raycastInput2 = new RaycastInput
                {
                    Start = translation.Value,
                    End = posPlayer,
                    Filter = Filter
                };
                if (physicsWorld.CollisionWorld.CastRay(raycastInput2, out var hit))
                {
                    if (Player.Components.HasComponent(hit.Entity))
                    {
                        if (math.distance(translation.Value, posPlayer) > 20)
                        {
                            pathFollow.ennemyState = EnnemyState.Chase;
                        }
                        else
                        {
                            pathFollow.ennemyState = EnnemyState.Attack;
                        }
                    }
                    else
                    {
                        pathFollow.ennemyState = EnnemyState.Wondering;
                    }
                }
            }
            else
            {
                pathFollow.ennemyState = EnnemyState.Wondering;
            }
        }).ScheduleParallel(Dependency).Complete();
    }

    private static void ChaseFollow(ref PathFollowComponent pathFollow, in Translation translation, in float3 posPlayer,
        ref PhysicsWorld physicsWorld, in DynamicBuffer<PathPosition> pathPos, ref ComponentDataContainer<PlayerTag> em)
    {
        pathFollow.PositionToGo = new int2(-1);
        int compteur = 0;
        bool loopBreak = false;
        while ((compteur != pathPos.Length && compteur != 4) && pathPos.Length > 0)
        {
            int2 basePos = pathPos[pathFollow.pathIndex].position;
            int2 pathPosition;
            if (pathFollow.pathIndex - compteur > 0)
                pathPosition = pathPos[pathFollow.pathIndex - compteur].position;
            else
                pathPosition = pathPos[0].position;

            float3 pathPositionConvert = new float3(pathPosition.x, 0.2f, pathPosition.y);
            float3 basePosConvert = new float3(basePos.x, 0.2f, basePos.y);
            RaycastInput raycastInput = new RaycastInput
            {
                Start = basePosConvert,
                End = pathPositionConvert,
                Filter = Filter
            };
            if (physicsWorld.CollisionWorld.CastRay(raycastInput, out var hit))
            {
                if (!em.Components.HasComponent(hit.Entity))
                {
                    if ((pathFollow.pathIndex - compteur + 1) != pathPos.Length)
                        pathFollow.PositionToGo = pathPos[(pathFollow.pathIndex - compteur) + 1].position;
                    else
                        pathFollow.PositionToGo = pathPos[(pathFollow.pathIndex - compteur)].position;
                    pathFollow.EnemyReachedTarget = false;
                    loopBreak = true;
                    break;
                }
            }

            compteur++;
        }

        if (!loopBreak)
        {
            if (pathPos.Length != 0)
            {
                if (pathFollow.pathIndex - compteur < 0)
                {
                    pathFollow.PositionToGo = pathPos[0].position;
                }

                pathFollow.PositionToGo = pathPos[(pathFollow.pathIndex - compteur) + 1].position;
            }

            pathFollow.EnemyReachedTarget = false;
        }
    }
    private static void WonderingFollow(ref PathFollowComponent pathFollow,ref PhysicsWorld physicsWorld, in Translation translation, in double time, in int entityInQueryIndex,in float deltaTime)
    {
        if (pathFollow.timeWonderingCounter < 0)
        {
            //Get next seed
            var rSeed = new Random((uint)( time + entityInQueryIndex + 1 ));
            
            //Get random Angle, distance and time to wonder
            int randomAngle = rSeed.NextInt(0, 360);
            int rayDistance = rSeed.NextInt(3, 7);
            pathFollow.timeWonderingCounter = rSeed.NextInt(1, 6);
            
            //Set the angle of wondering
            float angle = math.radians(randomAngle);
            float2 pos = new float2(math.cos(angle), math.sin(angle) * rayDistance);
            pathFollow.PositionToGo = (int2)(translation.Value.xz + pos);
            
            //Check if it collides with anything
            RaycastInput raycastInput = new RaycastInput
            {
                Start = translation.Value,
                End = new float3(pathFollow.PositionToGo.x, 0.5f, pathFollow.PositionToGo.y),
                Filter = Filter
            };
            //If collides, do not move
            if (physicsWorld.CollisionWorld.CastRay(raycastInput))
            {
                pathFollow.PositionToGo = new int2(-1);
                pathFollow.timeWonderingCounter = 0;
            }
        }
        else
        {
            pathFollow.timeWonderingCounter -= deltaTime;
        }
    }

    private static void AttackFollow(ref PathFollowComponent pathFollow, float3 pos, in Translation translation,
        ref AttackRangeComponent range)
    {
        if (math.distance(pos, translation.Value) >= range.Distance)
            pathFollow.PositionToGo = (int2) pos.xz;
        else
        {
            range.IsInRange = true;
        }
    }
}