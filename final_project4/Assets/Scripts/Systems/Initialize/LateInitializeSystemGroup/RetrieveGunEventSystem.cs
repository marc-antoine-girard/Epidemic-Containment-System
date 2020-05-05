﻿using Enums;
using EventStruct;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

struct EmptyEventQueueJob : IJob
{
    public NativeQueue<WeaponInfo> EventsQueue;

    public void Execute()
    {
        while (EventsQueue.TryDequeue(out WeaponInfo info))
        {
            EventsHolder.WeaponEvents.Add(info);
        }
    }
}

[DisableAutoCreation]
public class RetrieveGunEventSystem : SystemBase
{
    private EndInitializationEntityCommandBufferSystem entityCommandBuffer;
    private NativeQueue<WeaponInfo> weaponFired;

    protected override void OnCreate()
    {
        entityCommandBuffer = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        if (entityCommandBuffer == null)
        {
#if UNITY_EDITOR
            Debug.Log("GET DOWN! Problem incoming...");
#endif
        }

        weaponFired = new NativeQueue<WeaponInfo>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        weaponFired.Dispose();
    }

    protected override void OnUpdate()
    {
        //Create parallel writer
        NativeQueue<WeaponInfo>.ParallelWriter weaponFiredEvents = weaponFired.AsParallelWriter();

        //Create ECB
        EntityCommandBuffer.Concurrent ecb = entityCommandBuffer.CreateCommandBuffer().ToConcurrent();

        //Get all StateData components
        ComponentDataContainer<StateComponent> states = new ComponentDataContainer<StateComponent>
        {
            Components = GetComponentDataFromEntity<StateComponent>()
        };
        ComponentDataContainer<Translation> parentTranslations = new ComponentDataContainer<Translation>
        {
            Components = GetComponentDataFromEntity<Translation>()
        };

        float deltaTime = Time.DeltaTime;

        JobHandle gunJob = Entities.ForEach(
            (Entity e, int entityInQueryIndex, ref GunComponent gun, in LocalToWorld transform, in Parent parent) =>
            {
                //Make sure SwapDelay < 0
                if (gun.SwapTimer > 0)
                {
                    gun.SwapTimer -= deltaTime;
                    return;
                }

                //Make sure gun has a parent
                if (!states.Components.HasComponent(parent.Value))
                    return;
                //Make sure parent has a Translation
                if (!parentTranslations.Components.HasComponent(parent.Value))
                    return;

                //Variables local to job
                StateComponent state = states.Components[parent.Value];
                Translation position = parentTranslations.Components[parent.Value];
                WeaponInfo.WeaponEventType? weaponEventType = null;

                if (gun.IsReloading)
                {
                    //Decrease time
                    gun.ReloadTime -= deltaTime;

                    if (!gun.IsReloading)
                    {
                        Reload(ref gun);
                    }
                }
                //Only if not reloading
                else if (gun.IsBetweenShot)
                {
                    //Decrease time
                    gun.BetweenShotTime -= deltaTime;
                }

                if (state.CurrentState == State.Reloading)
                    if (TryReload(ref gun))
                        weaponEventType = WeaponInfo.WeaponEventType.ON_RELOAD;

                //Should weapon be reloading?    //Deactivate this line to block auto reload
                if (TryStartReload(ref gun))
                    weaponEventType = WeaponInfo.WeaponEventType.ON_RELOAD;

                if (state.CurrentState == State.Attacking)
                    if (TryShoot(ref gun))
                    {
                        weaponEventType = WeaponInfo.WeaponEventType.ON_SHOOT;
                        Shoot(entityInQueryIndex, ecb, ref gun, transform, position.Value);
                    }

                //Add event to NativeQueue
                if (weaponEventType != null)
                {
                    weaponFiredEvents.Enqueue(new WeaponInfo
                    {
                        WeaponType = gun.WeaponType,
                        EventType = (WeaponInfo.WeaponEventType) weaponEventType,
                        Position = transform.Position,
                        Rotation = transform.Rotation
                    });
                }
            }).ScheduleParallel(Dependency);

        //Create job
        JobHandle emptyEventQueueJob = new EmptyEventQueueJob
        {
            EventsQueue = weaponFired
        }.Schedule(gunJob);

        //Link all jobs
        Dependency = JobHandle.CombineDependencies(gunJob, emptyEventQueueJob);
        entityCommandBuffer.AddJobHandleForProducer(Dependency);
    }

    //Returns true if weapons starts reloading
    private static bool TryStartReload(ref GunComponent gun)
    {
        //Check if still ammo in magazine
        if (gun.CurrentAmountBulletInMagazine > 0)
            return false;
        //Make sure gun isnt reloading already
        if (gun.IsReloading)
            return false;
        //Make sure there is ammo to reload
        if (gun.CurrentAmountBulletOnPlayer <= 0)
            return false;

        //
        StartReload(ref gun);
        return true;
    }

    //Returns true if weapons starts reloading
    private static bool TryReload(ref GunComponent gun)
    {
        //Make sure magazine isnt full yet
        if (gun.CurrentAmountBulletInMagazine == gun.MaxBulletInMagazine)
            return false;
        //Make sure gun isnt reloading already
        if (gun.IsReloading)
            return false;
        //Make sure there is ammo to reload
        if (gun.CurrentAmountBulletOnPlayer <= 0)
            return false;

        StartReload(ref gun);
        return true;
    }

    private static void StartReload(ref GunComponent gun)
    {
        gun.ReloadTime = gun.ResetReloadTime;
    }

    private static void Reload(ref GunComponent gun)
    {
        int amountAmmoToPutInMagazine = gun.MaxBulletInMagazine;

        //Make sure enough ammo on player to refill entire magazine
        if (gun.CurrentAmountBulletOnPlayer < gun.MaxBulletInMagazine)
        {
            amountAmmoToPutInMagazine = gun.CurrentAmountBulletOnPlayer;
        }

        //
        gun.CurrentAmountBulletOnPlayer -= amountAmmoToPutInMagazine;
        gun.CurrentAmountBulletInMagazine = amountAmmoToPutInMagazine;
    }

    //Returns true if weapons shoot
    private static bool TryShoot(ref GunComponent gun)
    {
        //Make sure not reloading or not between shot
        if (gun.IsBetweenShot || gun.IsReloading)
            return false;
        //Make sure magazine isnt empty
        if (gun.CurrentAmountBulletInMagazine <= 0)
            return false;

        return true;
    }

    private static void Shoot(int jobIndex, EntityCommandBuffer.Concurrent ecb, ref GunComponent gun,
        in LocalToWorld transform, float3 parentEntityPosition)
    {
        //Decrease bullets
        gun.CurrentAmountBulletInMagazine--;

        //Reset between shot timer
        gun.BetweenShotTime = gun.ResetBetweenShotTime - gun.BetweenShotTime;

        switch (gun.WeaponType)
        {
            case WeaponType.Machinegun:
            case WeaponType.Pistol:
                ShootPistol(jobIndex, ecb, gun.BulletPrefab, transform.Position, transform.Rotation, parentEntityPosition, transform);
                break;
            case WeaponType.Shotgun:
                ShootShotgun(jobIndex, ecb, gun.BulletPrefab, transform.Position, transform.Rotation, parentEntityPosition);
                break;
            case WeaponType.PigWeapon:
                ShootPigWeapon(jobIndex, ecb, gun.BulletPrefab, transform.Position, transform.Rotation, parentEntityPosition);
                break;
            case WeaponType.GorillaWeapon:
                ShootGorillaWeapon(jobIndex, ecb, gun.BulletPrefab, transform.Position, transform.Rotation, parentEntityPosition);
                break;
        }
    }

    private static void ShootPistol(int jobIndex, EntityCommandBuffer.Concurrent ecb, Entity bulletPrefab,
        float3 position, quaternion rotation, float3 parentEntityPosition, LocalToWorld localToWorld)
    {
        //Create entity with prefab
        Entity bullet = ecb.Instantiate(jobIndex, bulletPrefab);

        //Set position/rotation
        ecb.SetComponent(jobIndex, bullet, new Translation
        {
            Value = position
        });
        ecb.SetComponent(jobIndex, bullet, new Rotation
        {
            Value = rotation
        });
        ecb.SetComponent(jobIndex, bullet ,new LocalToWorld
        {
            Value = localToWorld.Value
        });
        ecb.AddComponent(jobIndex, bullet, new BulletPreviousPositionData
        {
            Value = parentEntityPosition
        });
    }

    private static void ShootShotgun(int jobIndex, EntityCommandBuffer.Concurrent ecb, Entity bulletPrefab,
        float3 position, quaternion rotation, float3 parentEntityPosition)
    {
        int nbBullet = 100;
        float degreeFarShot = math.radians(nbBullet * 2);
        float angle = degreeFarShot / nbBullet;
        quaternion startRotation = math.mul(rotation, quaternion.RotateY(-(degreeFarShot / 2)));

        for (int i = 0; i < nbBullet; i++)
        {
            Entity bullet = ecb.Instantiate(jobIndex, bulletPrefab);

            //Find rotation
            quaternion bulletRotation = math.mul(startRotation, quaternion.RotateY(angle * i));

            //Set position/rotation
            ecb.SetComponent(jobIndex, bullet, new Translation
            {
                Value = position
            });
            ecb.SetComponent(jobIndex, bullet, new Rotation
            {
                Value = bulletRotation
            });
            ecb.AddComponent(jobIndex, bullet, new BulletPreviousPositionData
            {
                Value = parentEntityPosition
            });
        }
    }

    private static void ShootPigWeapon(int jobIndex, EntityCommandBuffer.Concurrent ecb, Entity bulletPrefab,
        float3 position, quaternion rotation, float3 parentEntityPosition)
    {
        int nbBullet = 3;
        float degreeFarShot = math.radians(nbBullet * 2);
        float angle = degreeFarShot / nbBullet;
        quaternion startRotation = math.mul(rotation, quaternion.RotateY(-(degreeFarShot / 2)));

        for (int i = 0; i < nbBullet; i++)
        {
            Entity bullet = ecb.Instantiate(jobIndex, bulletPrefab);

            //Find rotation
            quaternion bulletRotation = math.mul(startRotation, quaternion.RotateY(angle * i));

            //Set position/rotation
            ecb.SetComponent(jobIndex, bullet, new Translation
            {
                Value = position
            });
            ecb.SetComponent(jobIndex, bullet, new Rotation
            {
                Value = bulletRotation
            });
            ecb.AddComponent(jobIndex, bullet, new BulletPreviousPositionData
            {
                Value = parentEntityPosition
            });
        }
    }

    private static void ShootGorillaWeapon(int jobIndex, EntityCommandBuffer.Concurrent ecb, Entity bulletPrefab,
        float3 position, quaternion rotation, float3 parentEntityPosition)
    {
        int nbBullet = 15;
        float angle = 360f / nbBullet;

        for (int i = 0; i < nbBullet; i++)
        {
            Entity bullet = ecb.Instantiate(jobIndex, bulletPrefab);

            //Find rotation
            quaternion bulletRotation = math.mul(rotation, quaternion.RotateY(angle * i));

            //Set position/rotation
            ecb.SetComponent(jobIndex, bullet, new Translation
            {
                Value = position
            });
            ecb.SetComponent(jobIndex, bullet, new Rotation
            {
                Value = bulletRotation
            });
        }
    }
}