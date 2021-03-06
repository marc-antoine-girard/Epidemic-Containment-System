﻿using Unity.Collections;
using EventStruct;

public static class EventsHolder
{
    public static LevelInfo LevelEvents;

    public static NativeList<WeaponInfo> WeaponEvents =
        new NativeList<WeaponInfo>(Allocator.Persistent);
    
    public static NativeList<BulletInfo> BulletsEvents =
        new NativeList<BulletInfo>(Allocator.Persistent);
    
    public static NativeList<PlayerInfo> PlayerEvents =
        new NativeList<PlayerInfo>(Allocator.Persistent);
    
    public static NativeList<StateInfo> StateEvents = 
        new NativeList<StateInfo>(Allocator.Persistent);
    
    public static NativeQueue<AnimationInfo> AnimationEvents =
        new NativeQueue<AnimationInfo>(Allocator.Persistent);
    
    public static NativeList<InteractableInfo> InteractableEvents =
        new NativeList<InteractableInfo>(Allocator.Persistent);

    public static void OnDestroy()
    {
        WeaponEvents.Dispose();
        BulletsEvents.Dispose();
        PlayerEvents.Dispose();
        StateEvents.Dispose();
        AnimationEvents.Dispose();
        InteractableEvents.Dispose();
        //LevelEvents.Dispose();
    }
}