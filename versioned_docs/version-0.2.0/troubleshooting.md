---
sidebar_position: 5
---

# Troubleshooting

## An intent is set but never consumed

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either nothing reads this field, or its reader runs before its writer in the same step.
A writer in FixedUpdate must execute before its readers.
```

Work through the causes in order:

1. **Nothing reads the field.** Every discrete intent on a registered surface needs a reader that calls `TryConsume`, or `Clear` if it decides not to act.
2. **The reader runs before the writer.** A writer in `FixedUpdate` must run before its readers in the same step. Give it an earlier execution order, such as `[DefaultExecutionOrder(-10)]`, or move it to `Update`, where it binds to the next step instead. See [The Tick](./advanced-usage/the-tick.md).
3. **The reader only peeks.** `Peek` never consumes. The code that acts must call `TryConsume`.
4. **The window is wider than the field declares.** A reader that passes `window: 5` to a field without `[FixedInputWindow(5)]` gets reported the step after each press, even though the press was used. Declare the widest window any reader passes. See [Diagnostics](./advanced-usage/diagnostics.md#declaring-a-fields-window).
5. **The press legitimately went unused**, for example a jump pressed mid-air that expired before landing. If that is the design, ignore the message or widen the window.

In the Entities version of this message, the cause is the same with systems instead of components: a missing reader system, or a reader ordered before its writer in the same group.

## An intent was set twice in one step

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set twice during tick 4120, by two different writers:
```

Two writers are live on the same surface. The usual cause is a scripted writer driving a character whose device writer is still enabled. Enable exactly one writer at a time.

If the message says both sets came **from the same place**, it is a single writer in `Update` running twice before one fixed step, because the frame rate outruns the fixed rate. The first press is overwritten. That is harmless for most games; move the writer to `FixedUpdate` if every press must count.

## The tick is frozen

```
[MyFixedTick] Fixed steps are running but the tick is frozen at 812. Another package almost certainly
replaced the player loop and dropped this system, ...
```

Another package called `PlayerLoop.SetPlayerLoop` with a snapshot taken before the default tick source installed, and dropped its increment. Every intent will expire unconsumed. Either:

* call `PlayerLoopTickSource.Reinstall()` once the other package has installed itself, or
* use `MonoBehaviourTickSource`, which touches no engine internals, or another [tick source](./advanced-usage/tick-sources.md) the project controls.

## No FixedUpdate phase found

```
[PlayerLoopTickSource] No FixedUpdate phase found in the player loop, so the tick cannot advance.
```

The project's player loop has no fixed-update phase, so the default source has nowhere to install. Set `MyFixedTick.Source` to a [source](./advanced-usage/tick-sources.md) driven by whatever steps the simulation.

## Ages and ticks drift in a hybrid Entities project

Unity's fixed timestep defaults to 50 Hz and `FixedStepSimulationSystemGroup` to 60 Hz, so a hybrid project has two counters that drift hundreds of steps apart within a minute. Make one clock authoritative: match the timesteps and set `MyFixedTick.Source = new EntitiesTickSource(world)`. See [Entities](./advanced-usage/entities.md#hybrid-projects-one-clock).

## `MyFixedTick.Current` is always 0

Outside play mode, the default source reads `0`, because no fixed step is running. In play mode, a `0` that never moves means a custom source that is not advancing, or an `EntitiesTickSource` whose world has not created the tick singleton yet.

## A consumed intent stays armed

`FixedInputEvent` is a struct, so consuming a copy leaves the original armed:

```cs
var attack = input.Attack;
attack.TryConsume(tick);        // consumes the copy

input.Attack.TryConsume(tick);  // consumes the field
```

The same applies in Entities: consume through `RefRW<T>.ValueRW`, not `ValueRO`.

## The character keeps moving after swapping writers

The disabled writer left its last continuous value on the surface. Clear the fields a writer owns when it is disabled:

```cs
void OnDisable() => _input.Move = Vector2.zero;
```

## Reading the keyboard in FixedUpdate logs an error

With the Input System in its default update mode, device state is only valid in `Update`. Poll devices in the phase your Input System updates in, and stamp the tick that phase gives you. See [Latency](./advanced-usage/latency.md#which-phase-may-poll-a-device).
