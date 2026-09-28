# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this package
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-09-12

First release.

### Added

- `FixedInputEvent`, a one-shot input intent bound to a single fixed step. Two unmanaged fields in
  every build configuration, so it works as an `IComponentData` and from Burst-compiled code.
  - `Peek` to test without consuming, `TryConsume` to act, per-read buffering windows on both.
  - The window comparison uses wrapping subtraction, so it stays correct across the `uint` wrap and
    treats an intent bound to a future step as not yet visible.
- `ITickSource`, with three implementations:
  - `PlayerLoopTickSource`, the default. Installs its increment at the end of the fixed-update
    phase, so physics callbacks and coroutines resuming from `WaitForFixedUpdate` observe the step
    they are in. Needs no scene object and no script execution order.
  - `MonoBehaviourTickSource`, for a project whose player loop is contested.
  - `ManualTickSource`, for tests and deterministic replay.
- `MyFixedTick`, a settable ambient source, so a project can run off a clock it already owns.
- `FixedInputDiagnostics`. In the editor and in builds with `UNITY_ENABLE_CHECKS`, reports an intent nobody consumed
  and two writers setting one intent in a step, naming the field and both call sites.
- `FixedInputWindowAttribute`, declaring how long a field may stay armed before the diagnostics
  call it lost.
- A UI Toolkit inspector row showing armed state and age in ticks rather than a raw integer.
- Optional Entities support, compiled only when the Entities package is present. Adds an ECS clock
  and an entity diagnostic that needs no registration.
- `InputSurfacePanel`, a runtime UI Toolkit overlay docked in a corner: the tick, optional key
  caps, a rolling per-step lane for every intent on a surface, and a frame strip carrying the
  measured rate, the steps-to-frames ratio, a render rate cap and a time scale. The lanes are what make a frame-exact intent legible at
  all, since one is armed for twenty milliseconds. The rate cap is how you prove frame independence
  rather than assume it; both it and the time scale are restored when the overlay is disabled.
- `IntentTrack`, the timeline's classification, which reconstructs writes that were already consumed
  by the time the step closed. Engine-free and directly tested.
- `MyFixedTick.StepClosing`, raised after every reader in a step has run and before the count
  advances, so tooling can observe a surface without interfering.
- Two samples, one MonoBehaviour and one Entities, each driving a single body from a device writer
  and a scripted writer through one surface, both rendering the same panel.

### Known limitations

- Entities keeps its inspector extension points internal, so the inspector row is not available in
  the entity inspector. Authoring components are unaffected.
- Unity's fixed timestep and `FixedStepSimulationSystemGroup` default to different rates, so a
  hybrid project runs two clocks unless one is made authoritative.
