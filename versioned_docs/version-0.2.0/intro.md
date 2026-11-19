---
sidebar_position: 1
---

# Introduction

**My Fixed Tick** is a Unity package for a **deterministic fixed-step tick** and a **one-shot input intent** bound to a single step. An intent is valid for exactly one fixed simulation step and expires by arithmetic: nothing clears it, nothing can consume it twice, and a paused or reloaded game cannot deliver a stale press.

The point is what that makes possible. Put a project's input vocabulary in a plain type, have every reader read only that, and a character driven by a keyboard and the same character driven by a script run the identical code. Every mechanic written for a player works for an agent the day it ships.

## Key Features

* **One-Shot Input Intents**: `FixedInputEvent` is valid for one fixed step, with a peek/consume split and per-read buffering windows. Two unmanaged fields, so it works as an `IComponentData` and from Burst-compiled code.
* **A Tick That Installs Itself**: The default source installs into the `PlayerLoop` at the end of the fixed phase, so it needs no scene object and no script execution order.
* **Swappable Clocks**: `ITickSource` is an interface, so a project can run off a clock it already owns.
* **Diagnostics That Name The Problem**: The editor and development builds report an intent nobody consumed and two writers that fought over one field.
* **Optional Entities Support**: Compiled only when the Entities package is present.

## Installation

To get started with My Fixed Tick, you can install it in various ways:

* [OpenUPM](./getting-started/installation.mdx#openupm)
* [Install from Git](./getting-started/installation.mdx#git)
* [Install from Tarball](./getting-started/installation.mdx#tarball)
* [Unity Package](./getting-started/installation.mdx#unitypackage)

## Quick Start

Declare an input surface, with one field per intent:

```cs
using MyGameDevTools.FixedInput;
// [...]

public class CharacterInput : MonoBehaviour
{
    public Vector2 Move;                                  // continuous
    [FixedInputWindow(5)] public FixedInputEvent Attack;  // discrete, lenient
    public FixedInputEvent Evade;                         // discrete, frame-exact
}
```

A writer sets an intent for the current tick, wherever its input comes from:

```cs
input.Attack.Set(MyFixedTick.Current);
```

A reader consumes it in the fixed step it was set for:

```cs
void FixedUpdate()
{
    uint tick = MyFixedTick.Current;

    if (input.Attack.TryConsume(tick, window: 5))
        Attack();
}
```

That replaces the usual pattern:

```cs
bool _attackPressed;

void Update()
{
    if (Keyboard.current.jKey.wasPressedThisFrame)
        _attackPressed = true;
}

void FixedUpdate()
{
    if (_attackPressed)
    {
        _attackPressed = false;
        Attack();
    }
}
```

That flag stays set until something clears it, so a paused or reloaded game delivers a stale press, a second reader never sees it, and the whole thing depends on a script execution order nobody wrote down.

:::info
Continue with [Basic Usage](./getting-started/basic-usage.md) for a full writer and reader, or read [The Input Surface](./advanced-usage/input-surface.md) for the six rules that make a surface swappable.
:::
