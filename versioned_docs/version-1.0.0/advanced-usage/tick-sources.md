---
sidebar_position: 4
description: The tick sources the package ships, and how to supply your own.
---

# Tick Sources

A tick source is anything that counts fixed simulation steps:

```cs
public interface ITickSource
{
    /// <summary>The step that will consume intents set right now.</summary>
    uint Tick { get; }
}
```

`MyFixedTick.Source` holds the one every unwired reader uses. The package ships three.

| Source | Use it for |
|---|---|
| `PlayerLoopTickSource` | The default. Nothing to set up. |
| `MonoBehaviourTickSource` | A project whose player loop is contested by other packages. |
| `ManualTickSource` | Tests and deterministic replay. |

## PlayerLoopTickSource

The default, and it installs itself: there is no component to add, no prefab to wire, and no script execution order to get right. When play mode starts, it appends its increment to the **end** of the fixed-update phase of the current player loop, which is why physics callbacks and coroutines resuming from `WaitForFixedUpdate` observe the step they are actually in. When play mode ends, it removes its increment and leaves every other system untouched.

| Member | |
|---|---|
| `Instance` | The shared instance. |
| `IsInstalled` | Whether its system is currently present in the player loop. |
| `Reinstall()` | Puts the increment back at the end of the fixed-update phase. |

### When another package replaces the player loop

A package that calls `PlayerLoop.SetPlayerLoop` with a snapshot taken before this one installed drops the increment. Fixed steps keep running, the tick stops, and every intent expires unconsumed.

In the editor and in development builds, `MyFixedTick.Current` notices, checks the player loop to be sure, and reports it once:

```
[MyFixedTick] Fixed steps are running but the tick is frozen at 812. Another package almost certainly
replaced the player loop and dropped this system, ...
```

Call `PlayerLoopTickSource.Reinstall()` after the other package has installed itself, or switch to `MonoBehaviourTickSource`, which touches no engine internals.

## MonoBehaviourTickSource

The escape hatch for a contested player loop. Add **Fixed Input/MonoBehaviour Tick Source** to an object in your first scene. It counts in `FixedUpdate` at execution order `32000`, so every ordinary writer and reader has already run in that step.

While enabled, it registers itself as `MyFixedTick.Source`, and hands the slot back to whatever was there before when it is disabled. Untick **Install As Ambient Source** to keep it out of `MyFixedTick` and pass it around yourself.

The trade is that it increments inside the script phase, before physics, so a physics callback during step `T` sees `T + 1`. It also needs to exist in the scene, and to survive scene loads if the clock should too.

## ManualTickSource

A source driven by hand, for tests and deterministic replay:

```cs
var clock = new ManualTickSource(startTick: 100);
MyFixedTick.Source = clock;

input.Attack.Set(MyFixedTick.Current);   // tick 100
Assert.IsTrue(input.Attack.Peek(clock.Tick));

clock.Advance();                         // tick 101
Assert.IsFalse(input.Attack.Peek(clock.Tick));
Assert.IsTrue(input.Attack.Peek(clock.Tick, window: 1));
```

| Member | |
|---|---|
| `Advance()` | Closes the current step and moves to the next one. |
| `Advance(steps)` | Closes several steps at once. |
| `SetTick(tick)` | Jumps to an arbitrary step, for example near `uint.MaxValue` to test the wrap. |

Call `MyFixedTick.ResetToDefault()` in a test's teardown. Entering play mode resets the source anyway, so a forgotten one cannot leak into the next session.

## Your own source

Implement `ITickSource` over a clock the project already owns, such as a netcode tick or a lockstep frame, and set it as the ambient source:

```cs
public sealed class NetcodeTickSource : ITickSource
{
    readonly MyNetworkClock _clock;

    public NetcodeTickSource(MyNetworkClock clock) => _clock = clock;

    public uint Tick => _clock.CurrentSimulationTick;
}

MyFixedTick.Source = new NetcodeTickSource(clock);
```

Keep the [definition](./the-tick.md) intact: `Tick` names the step that will consume intents set right now, and advances once that step's readers have run.

:::warning
A custom source does not raise `MyFixedTick.StepClosing` or run the unconsumed-intent check, because both live in the shipped sources. Call `FixedInputDiagnostics.CheckForUnconsumed(tick)` yourself where your clock's step closes, before it advances; tooling that samples on `StepClosing` has to be driven from the same place.
:::
