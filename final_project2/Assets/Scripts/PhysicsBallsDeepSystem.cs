﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;
using UnityEngine;
using static Unity.Mathematics.math;

public class PhysicsBallsDeepSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities.ForEach((in InputComponent i) => { }).Schedule();
    }
}