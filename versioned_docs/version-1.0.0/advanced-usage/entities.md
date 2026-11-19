---
sidebar_position: 7
description: The optional Entities clock, intents in components, and the entity diagnostic.
---

# Entities

The package has no Entities dependency. When `com.unity.entities` is present, an extra assembly, `MyGameDevTools.FixedInput.Entities`, compiles itself in and adds an ECS clock:

* `FixedTickSingleton`, advanced by a system ordered last in `FixedStepSimulationSystemGroup`.
* `EntitiesTickSource`, so hybrid managed code can run off the same clock through `ITickSource`.

## Intents in components

`FixedInputEvent` needs nothing ECS-specific. It is two unmanaged fields and four methods of pure arithmetic, so it drops into an `IComponentData` and Burst compiles it:

```cs
using MyGameDevTools.FixedInput;
using Unity.Entities;
using Unity.Mathematics;

public struct CharacterInput : IComponentData
{
    public float2 Move;
    [FixedInputWindow(14)] public FixedInputEvent Attack;
    [FixedInputWindow(6)]  public FixedInputEvent Jump;
}
```

## The ECS tick

`FixedTickSingleton.Value` follows the same [definition](./the-tick.md) as `MyFixedTick.Current`: the step that will consume intents set right now. It is created along with the world and advanced at the end of each `FixedStepSimulationSystemGroup` update, after every reader.

A reader in the fixed-step group reads the singleton and consumes:

```cs
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct CharacterBodySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state) => state.RequireForUpdate<FixedTickSingleton>();

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        uint tick = SystemAPI.GetSingleton<FixedTickSingleton>().Value;

        foreach (RefRW<CharacterInput> input in SystemAPI.Query<RefRW<CharacterInput>>())
        {
            if (input.ValueRW.Jump.TryConsume(tick, 6))
            {
                // jump
            }
        }
    }
}
```

Use `RefRW` and `ValueRW` to consume. `ValueRO` returns a copy, and consuming a copy leaves the component armed.

The same writer positions apply as with MonoBehaviours:

| Writer runs in | Sees | Consumed |
|---|---|---|
| `FixedStepSimulationSystemGroup`, ordered before its readers | `T`, the step it is inside | later in step `T` |
| `InitializationSystemGroup`, once per frame | the next step to run, because the count advanced at the end of the last group update | in that step, the next time the group updates |

## Hybrid projects: one clock

:::warning[Two clocks by default]
Unity's fixed timestep defaults to 50 Hz and `FixedStepSimulationSystemGroup` to 60 Hz, and it runs its own catch-up loop rather than the engine's fixed phase. A hybrid project running both has two counters that drift apart.
:::

Pick one as authoritative and point `MyFixedTick.Source` at it. To make the ECS tick authoritative, match the two timesteps and install an `EntitiesTickSource`:

```cs
[DefaultExecutionOrder(-32000)]
public class EntitiesClock : MonoBehaviour
{
    void Awake()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = Time.fixedDeltaTime;
        MyFixedTick.Source = new EntitiesTickSource(world);
    }

    void OnDestroy() => MyFixedTick.ResetToDefault();
}
```

Without it, the two counts drift hundreds of steps apart within a minute. `new EntitiesTickSource()` without a world uses the default world, and reads `0` until the singleton exists.

## Diagnostics

The entity diagnostic is better than the managed one: it needs no registration at all. ECS publishes its own type layout, so the scan finds every component holding an intent, wherever inside the component it sits, including nested structs, and reads each field's `[FixedInputWindow]`.

```
[FixedInput] 'CharacterInput.Attack' on Entity(12:1) was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either no system reads this field, or its reader is ordered before its writer in the same step.
```

It runs in the editor and in builds with `UNITY_ENABLE_CHECKS`, at the end of the fixed-step group just before the tick advances, and disables itself when no component in the project holds an intent. `FixedInputDiagnostics.Enabled` and `DefaultWindow` apply to it too.

## Inspector

Entities keeps its inspector extension points internal, so there is no supported way to render the [inspector row](./inspector.md) in the entity inspector, which shows the raw fields. Authoring components use the normal drawer and are unaffected.
