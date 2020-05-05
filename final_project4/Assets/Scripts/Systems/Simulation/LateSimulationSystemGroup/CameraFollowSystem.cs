﻿using System.Linq;
using Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[DisableAutoCreation]
public class CameraFollowSystem : SystemBase
{
    private float length = 8;
    private float offsetRad = math.radians(-45f);

    protected override void OnCreate()
    {
    }

    protected override void OnUpdate()
    {
        //Get Player Translation, which was set by the physic system
         var currentPosition = EntityManager.GetComponentData<Translation>(GameVariables.Player.Entity).Value;
         

        //Calculate Input
        InputComponent input = EntityManager.GetComponentData<InputComponent>(GameVariables.Player.Entity);
        
        var screenPos = new float2(input.Mouse.x - Screen.width * 0.5f, input.Mouse.y - Screen.height * 0.5f) / 20;
        var distance = math.clamp(math.distance(0, screenPos), 0.1f, length);
        var oldPos = math.normalizesafe(screenPos);

        var newDir = float3.zero;
        var cos = math.cos(offsetRad);
        var sin = math.sin(offsetRad);
        newDir.x = oldPos.x * cos - oldPos.y * sin;
        newDir.z = oldPos.x * sin + oldPos.y * cos;

        GameVariables.MouseToTransform.position = currentPosition + newDir * distance;
        GameVariables.Player.Transform.position = currentPosition;
    }
}