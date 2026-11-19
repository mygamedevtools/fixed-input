---
sidebar_position: 6
description: Report intents nobody consumed and writers that fought over one field.
---

# Diagnostics

An input surface has two failures that are silent by nature: an intent nobody consumed, and two writers that set the same intent in one step. Both used to produce a character that mysteriously ignored input. `FixedInputDiagnostics` turns them into messages that name the field.

The checks run in the editor and in any player build that defines `UNITY_ENABLE_CHECKS`. Everything below compiles to nothing in a build without it, and none of it lives inside `FixedInputEvent`, which stays two fields in every configuration.

:::tip
To get the checks in a player build, add `UNITY_ENABLE_CHECKS` to **Project Settings > Player > Scripting Define Symbols** for that build.
:::

## Intents nobody consumed

Register a surface and any intent still armed after its window is reported by name:

```cs
void OnEnable()  => FixedInputDiagnostics.Register(this);
void OnDisable() => FixedInputDiagnostics.Unregister(this);
```

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either nothing reads this field, or its reader runs before its writer in the same step.
A writer in FixedUpdate must execute before its readers.
```

The check runs once per step, after every reader in it has had its chance. It finds every `FixedInputEvent` field on the surface by reflection, including private and inherited ones, and reports each incident once: the next report for that field waits until it has been consumed or cleared.

Registration holds a weak reference, so a surface that is garbage collected or destroyed drops out by itself. A surface does not have to be a `MonoBehaviour`; any object works, although a component gets its name and GameObject in the message and a click-to-select context.

### Declaring a field's window

The window lives at each read, so the check cannot see it. Without a declaration, a field is assumed frame-exact and a buffered press is reported as lost the step after it was set. Declare the widest window any reader passes:

```cs
[FixedInputWindow(5)] public FixedInputEvent Attack;
```

`FixedInputDiagnostics.DefaultWindow` sets the window assumed for every field without the attribute.

## Two writers in one step

Rule 4 allows exactly one writer per surface per frame. Call `NoteSet` beside `Set` and two writers in one step are reported with both call sites:

```cs
FixedInputDiagnostics.NoteSet(surface, nameof(surface.Attack), tick);
surface.Attack.Set(tick);
```

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set twice during tick 4120, by two different writers:
  Assets/Scripts/KeyboardWriter.cs:31
  Assets/Scripts/AgentWriter.cs:44
Exactly one writer may fill an input surface per frame. ...
```

The call site is filled in by the compiler, so there is nothing else to pass.

When both sets come from the **same** place, it is a warning instead: a writer in `Update` that runs twice before a fixed step, because the frame rate outruns the fixed rate, overwrites its first press. Move it to `FixedUpdate` if every press must count.

## Switching it off

```cs
FixedInputDiagnostics.Enabled = false;
```

`Enabled` defaults to `true` wherever the checks are compiled in. Entering play mode restores it, forgets every registered surface and resets `DefaultWindow` to `0`.

## Entities

The entity diagnostic needs no registration at all. ECS publishes its own type layout, so a system finds every component holding a `FixedInputEvent`, wherever inside the component it sits, and reads `[FixedInputWindow]` from those fields. See [Entities](./entities.md#diagnostics).

## The player loop

One more check lives in `MyFixedTick`: if fixed steps are running but the tick is not advancing, because another package replaced the player loop and dropped the default source, it says so once. See [Tick Sources](./tick-sources.md#when-another-package-replaces-the-player-loop).

## API

| Member | |
|---|---|
| `Register(surface)` | Watches a surface for intents nobody consumes. |
| `Unregister(surface)` | Stops watching a surface. |
| `NoteSet(surface, field, tick)` | Records a write, so a second writer in the same step can be named. |
| `CheckForUnconsumed(tick)` | Reports every registered intent armed past its window. The shipped tick sources call it; a [custom source](./tick-sources.md#your-own-source) should too. |
| `Enabled` | Whether the checks run. |
| `DefaultWindow` | The window assumed for a field without `[FixedInputWindow]`. |
