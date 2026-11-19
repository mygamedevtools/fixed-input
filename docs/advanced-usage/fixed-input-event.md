---
sidebar_position: 2
description: The one-shot input intent, its methods and buffering windows.
---

# Fixed Input Event

`FixedInputEvent` is a one-shot input intent bound to a single fixed simulation step. It is a struct with two fields, the tick it was set for and whether it is armed, so it has no allocation, no references and no engine state.

```cs
public struct FixedInputEvent
{
    public bool IsArmed { get; }   // set, and not yet consumed or cleared
    public uint SetTick { get; }   // the tick it was set for

    public void Set(uint tick);
    public bool Peek(uint tick, uint window = 0);
    public bool TryConsume(uint tick, uint window = 0);
    public void Clear();
}
```

| Member | |
|---|---|
| `Set(tick)` | Arms the intent for the step that should consume it. Setting it again overwrites the tick. |
| `Peek(tick, window)` | Whether the intent is visible at `tick`. Never changes it. |
| `TryConsume(tick, window)` | Disarms the intent and returns `true` if it is visible at `tick`; otherwise returns `false` and leaves it alone. |
| `Clear()` | Disarms the intent without acting on it. |

Because it is a struct, read and write it through the field, not through a copy:

```cs
input.Attack.TryConsume(tick);   // consumes the field

var attack = input.Attack;       // a copy
attack.TryConsume(tick);         // consumes the copy; the field is still armed
```

## Visibility

An intent set at tick `T` is visible during step `T`, or, when a reader passes a window, during steps `T` through `T + window`. Outside that range it is invisible, and it expires by arithmetic: nothing has to clear it.

The comparison is a wrapping subtraction, `tick - SetTick <= window`, which gives it two properties:

* It stays correct across the `uint` wrap.
* An intent set for a **future** step underflows to a huge age, so it stays invisible until its step arrives. This is what makes an `Update`-phase writer, which binds to the next step, safe.

## Peek and TryConsume

`Peek` tests. `TryConsume` acts. Keep them apart wherever a decision has more than one clause:

```cs
// Peek, because whether the attack may start depends on a second clause.
bool wantsAttack = input.Attack.Peek(tick, AttackBufferTicks);

bool canStart = !attacking;
bool canChain = attacking && InChainWindow(tick);

if (wantsAttack && (canStart || canChain))
{
    // Only now, at the point of acting, is the press consumed.
    input.Attack.TryConsume(tick, AttackBufferTicks);
    StartAttack(tick);
}
```

A condition that consumed while merely testing would eat the press whenever its own second clause failed, and a later condition would never see it.

## Buffering

The window is an argument to each read rather than a property of the intent, so one press can be strict for one reader and lenient for another:

```cs
input.Attack.Peek(tick)            // frame-exact
input.Attack.Peek(tick, window: 5) // ~100 ms at 50 Hz
```

Buffering is the whole reason `TryConsume` exists. Widen a window and an intent stays visible for several steps, so without explicit consumption a buffered attack fires once per step.

A window exists for **reader gating**: a press often arrives while the reader is not yet willing to act, mid-attack or mid-air. It does not make the press arrive earlier, it tolerates the wait. That is why widening one feels like removing lag even though the press arrived just as late. See [Latency](./latency.md).

### Sizing a window

A window is counted in fixed steps past the one the intent was set for, so how long it tolerates a wait depends on the fixed timestep:

| Window | Extra time at 50 Hz | Extra time at 60 Hz |
|---|---|---|
| `0` | none, its own step only | none, its own step only |
| `5` | 100 ms | ~83 ms |
| `14` | 280 ms | ~233 ms |

A press that must survive until a later opportunity, like the chain window at the end of an attack, needs a window at least as long as the wait. The [sample](../samples/input-surface-reference.md) buffers its attack for 14 steps for exactly that reason.

### Declaring the window

Since the window lives at the call site, the diagnostics cannot see it. Declare the widest window any reader passes on the field itself, so they know how long the field may stay armed before they call it lost:

```cs
[FixedInputWindow(14)] public FixedInputEvent Attack;
```

## In Entities and Burst

`FixedInputEvent` needs nothing ECS-specific. It is two unmanaged fields and four methods of pure arithmetic, so it drops into an `IComponentData` and Burst compiles it. See [Entities](./entities.md).

## Equality

Two intents are equal when both are idle, or both are armed for the same tick. A disarmed intent's stored tick is meaningless, so every idle value compares and hashes alike. `ToString()` returns `idle` or `armed @ tick <n>`.
