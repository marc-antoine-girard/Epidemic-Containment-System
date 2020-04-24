﻿using System;
using System.Collections.Generic;
using System.Linq;
using Enums;
using EventStruct;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Windows.Speech;


public static class SoundHolder
{
    public static Dictionary<int, AudioClip> Sounds;
    
    public static Dictionary<WeaponType, Dictionary<WeaponInfo.WeaponEventType, int>> WeaponSounds;
    public static Dictionary<ProjectileType, Dictionary<BulletInfo.BulletCollisionType, int>> BulletSounds;

    private static int currentNumberOfLoadedAssets;
    private static int numberOfAssetsToLoad;
    
    public static void Initialize()
    {
        numberOfAssetsToLoad = ScriptableSoundName.Length;
        currentNumberOfLoadedAssets = 0;
        
        WeaponSounds = new Dictionary<WeaponType, Dictionary<WeaponInfo.WeaponEventType, AudioClip>>();
        BulletSounds = new Dictionary<ProjectileType, Dictionary<BulletInfo.BulletCollisionType, AudioClip>>();

        Sounds = new Dictionary<int, AudioClip>();
        
        //Weapons
        WeaponSounds = new Dictionary<WeaponType, Dictionary<WeaponInfo.WeaponEventType, int>>();
        for (int i = 0; i < Enum.GetNames(typeof(WeaponType)).Length; i++)
        {
            WeaponSounds.Add((WeaponType) i, new Dictionary<WeaponInfo.WeaponEventType, int>());
        }

        //Bullets
        BulletSounds = new Dictionary<ProjectileType, Dictionary<BulletInfo.BulletCollisionType, int>>();
        for (int i = 0; i < Enum.GetNames(typeof(ProjectileType)).Length; i++)
        {
            BulletSounds.Add((ProjectileType) i, new Dictionary<BulletInfo.BulletCollisionType, int>());
        }
    }

    public static void LoadAssets()
    {
        //Get ScriptableObject SoundsContainer
        Addressables.LoadAssetAsync<SoundsContainer>("SoundsContainer").Completed += handle =>
        {
            ExtractDataFromContainer(handle.Result);
        };
    }

    private static void ExtractDataFromContainer(SoundsContainer container)
    {
        int nextClipID = 0;
        
        foreach (SoundLinksScriptableObjects links in container.SoundLinksList)
        {
            //Add AudioClip to dictionary
            Sounds.Add(nextClipID, links.Clip);
            
            //Weapons
            foreach (SoundLinksScriptableObjects.WeaponLinks weapon in links.Weapons)
            {
                //Add to weapon dictionary
                WeaponSounds[weapon.WeaponType].Add(weapon.EventType, nextClipID);
            }
            
            //Bullets
            foreach (SoundLinksScriptableObjects.BulletLinks bullet in links.Bullets)
            {
                //Add to bullet dictionary
                BulletSounds[bullet.BulletType].Add(bullet.CollisionType, nextClipID);
            }
            
            //Increment ID
            nextClipID++;
        }
    }
    
    public static float CurrentLoadingPercentage()
    {
        return (float) currentNumberOfLoadedAssets / numberOfAssetsToLoad;
    }
}