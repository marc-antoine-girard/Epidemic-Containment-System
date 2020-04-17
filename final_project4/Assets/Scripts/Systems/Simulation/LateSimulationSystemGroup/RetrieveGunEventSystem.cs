﻿using EventStruct;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;
[DisableAutoCreation]
public class RetrieveGunEventSystem : SystemBase
{
    private EntityCommandBufferSystem simulationECB;
    private NativeQueue<WeaponInfo> weaponFired;

    protected override void OnCreate()
    {
        simulationECB = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        if (simulationECB == null)
            Debug.Log("GET DOWN! Problem incoming...");
        weaponFired = new NativeQueue<WeaponInfo>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        weaponFired.Dispose();
    }

    protected override void OnUpdate()
    {
        //Clear previous PistolBullet events
        EventsHolder.WeaponEvents.Clear(); //TODO MOVE TO CLEANUPSYSTEM

        //Create parallel writer
        NativeQueue<WeaponInfo>.ParallelWriter weaponFiredEvents = weaponFired.AsParallelWriter();

        //Create ECB
        var ecb = simulationECB.CreateCommandBuffer().ToConcurrent();

        float deltaTime = Time.DeltaTime;
        
        Entities.ForEach(
            (int entityInQueryIndex, ref GunComponent gun, in LocalToWorld transform, in StateData state) =>
            {
                WeaponInfo.WeaponEventType weaponEventType = WeaponInfo.WeaponEventType.NONE;

                if (gun.IsReloading)
                {
                    gun.ReloadTime -= deltaTime;
                    if (gun.ReloadTime <= 0)
                    {
                        //Refill magazine
                        if (gun.CurrentAmountBulletOnPlayer >= gun.MaxBulletInMagazine)
                        {
                            gun.CurrentAmountBulletInMagazine = gun.MaxBulletInMagazine;
                            gun.CurrentAmountBulletOnPlayer -= gun.MaxBulletInMagazine;
                        }
                        else
                        {
                            gun.CurrentAmountBulletInMagazine = gun.CurrentAmountBulletOnPlayer;
                            gun.CurrentAmountBulletOnPlayer -= gun.CurrentAmountBulletOnPlayer;
                        }
                    }
                }
                else if (state.Value == StateActions.ATTACKING && !gun.IsBetweenShot &&
                         gun.CurrentAmountBulletInMagazine > 0)
                {
                    //Shoot event
                    gun.CurrentAmountBulletInMagazine--;

                    //Set EventType
                    weaponEventType = WeaponInfo.WeaponEventType.ON_SHOOT;

                    //Create entity in EntityCommandBuffer
                    //TODO GET PREFAB ENTITY LINK WITH GUNTYPE

                    ecb.SetComponent(entityInQueryIndex, gun.Bullet, new Translation
                    {
                        Value = transform.Position
                    });
                    ecb.SetComponent(entityInQueryIndex, gun.Bullet, new Rotation
                    {
                        Value = transform.Rotation
                    });
                    ecb.Instantiate(entityInQueryIndex, gun.Bullet);
                }
                
                else if (gun.CurrentAmountBulletInMagazine <= 0 && gun.CurrentAmountBulletOnPlayer > 0)
                {
                    //Reload event
                    gun.ReloadTime = gun.ResetReloadTime;

                    //Set EventType
                    weaponEventType = WeaponInfo.WeaponEventType.ON_RELOAD;
                }

                if (weaponEventType != WeaponInfo.WeaponEventType.NONE)
                    //Add event to NativeQueue
                    weaponFiredEvents.Enqueue(new WeaponInfo
                    {
                        GunType = gun.GunType,
                        EventType = weaponEventType,
                        Position = transform.Position,
                        Rotation = transform.Rotation
                    });
            }).ScheduleParallel();

        //Terminate job so we can read from NativeQueue
        // job.Complete();

        // //Transfer NativeQueue info to static NativeList
        // while (weaponFired.TryDequeue(out WeaponInfo info))
        // {
        //     EventsHolder.WeaponEvents.Add(info);
        // }

        simulationECB.AddJobHandleForProducer(Dependency);
    }
}