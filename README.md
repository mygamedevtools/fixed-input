# My Fixed Tick

A deterministic fixed-step tick for Unity, and a one-shot input intent bound to a single step.

```csharp
// A writer, wherever input comes from.
input.Attack.Set(MyFixedTick.Current);

// A reader, later in the same step.
if (input.Attack.TryConsume(tick))
    Attack();
```

An intent is valid for exactly one fixed simulation step and expires by arithmetic. Nothing clears
it, nothing can consume it twice, and a paused or reloaded game cannot deliver a stale press.

## Why it is worth having

The primitive is small. The thing built on it is not.

Put a project's input vocabulary in a plain type, have every reader read only that, and a character
driven by a keyboard and the same character driven by a script run the identical code. Every
mechanic written for a player works for an agent the day it ships, because there is no second path
to drift out of sync: no parallel agent attack, no agent-only movement, no mechanic that quietly
works for one of them and not the other.

That property is worth a contract, and the package ships one: six rules, each with the consequence
of breaking it, plus development-build diagnostics that report a violation by name instead of
leaving you with a character that mysteriously ignores input.

## Install

Unity 6000.0 or newer. No package dependencies.

**Package Manager → Add package from git URL:**

```
https://github.com/mygamedevtools/fixed-input.git?path=/Packages/com.mygamedevtools.fixed-input
```

Or add it to `Packages/manifest.json` directly:

```json
"com.mygamedevtools.fixed-input": "https://github.com/mygamedevtools/fixed-input.git?path=/Packages/com.mygamedevtools.fixed-input"
```

## What you get

- **`FixedInputEvent`**, a one-shot intent with a peek/consume split and per-read buffering windows.
  Two unmanaged fields, so it works as an `IComponentData` and from Burst-compiled code.
- **A tick source that installs itself** into the `PlayerLoop` at the end of the fixed phase, so it
  needs no scene object and no script execution order, and physics callbacks observe the step they
  are actually in. Swappable, because `ITickSource` is an interface.
- **Diagnostics** that name an intent nobody consumed and two writers that fought over one field.
- **A runtime overlay** with a rolling lane per intent, a frame-rate cap and a time scale, so you
  can prove frame independence rather than assume it.
- **Optional Entities support**, compiled only when that package is present.
- **Two samples**, MonoBehaviour and ECS, deliberately identical down to the numbers.

## Documentation

The [package README](Packages/com.mygamedevtools.fixed-input/README.md) is the real documentation.
It covers the [contract](Packages/com.mygamedevtools.fixed-input/README.md#the-contract), what
[the tick](Packages/com.mygamedevtools.fixed-input/README.md#the-tick) actually means, where
[latency](Packages/com.mygamedevtools.fixed-input/README.md#latency-and-which-parts-of-it-you-can-remove)
comes from and which parts of it you can remove, and the
[diagnostics](Packages/com.mygamedevtools.fixed-input/README.md#diagnostics).

## Repository layout

This repository is a Unity project that hosts the package, so the samples and tests have somewhere
to run.

| Path | |
|---|---|
| `Packages/com.mygamedevtools.fixed-input` | the package itself |
| `Packages/com.mygamedevtools.fixed-input/Samples` | both reference samples |
| `Packages/com.mygamedevtools.fixed-input/Tests` | 69 tests, edit mode and play mode |
| `Assets`, `ProjectSettings` | the host project |

`com.unity.entities` is a development dependency of the host project, not of the package. It is
there so the optional Entities assembly is compiled and tested; a consumer without Entities installs
nothing extra and compiles nothing extra.

## Licence

MIT. See [LICENSE.txt](Packages/com.mygamedevtools.fixed-input/LICENSE.txt).
