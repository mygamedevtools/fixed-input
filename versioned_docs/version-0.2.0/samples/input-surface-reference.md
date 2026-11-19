---
sidebar_position: 1
description: Learn with the Input Surface Reference sample.
---

# Input Surface Reference

This sample is one character driven by a keyboard and by a scripted agent through the **same input surface**. Press <kbd>Tab</kbd> to swap the writer at runtime and the character keeps running, because the code that moves it cannot tell which one is live. An overlay shows every intent step by step, and lets you cap the frame rate and scale time, so you can prove frame independence rather than assume it.

It ships as two scenes, one MonoBehaviour and one Entities, identical down to the numbers.

## Installation

Import the sample through the **Package Manager**.

1. Open `Window/Package Manager`.
2. Select `My Fixed Tick` from the `In Project` list.
3. In the right panel, select the **Samples** tab.
4. Click on the `Import` button on the `Input Surface Reference` item.

The sample assets will be installed to `Samples/My Fixed Tick/<version>/Input Surface Reference`.

The sample needs the **Input System** package, and its Entities scene also needs **Entities**. See [Compatibility](../getting-started/compatibility.md#samples).

## Scriptable Render Pipeline Compatibility

The materials use the built-in Standard shader, so they render in the built-in pipeline as they are. When importing the sample into a project with an active **Scriptable Render Pipeline**, a dialog will appear asking if you want to automatically upgrade the sample materials.
It can upgrade them for either **URP** or **HDRP**.

## Playing the Sample

Open either scene in `Scenes/` and enter play mode:

- **InputSurfaceReference** — the MonoBehaviour scene.
- **InputSurfaceReferenceEntities** — the same demonstration as ECS systems. The entities create themselves from a system, so there is no subscene to bake, and a presentation component mirrors each one onto an animated GameObject, so the scene needs no Entities Graphics package.

| Key | Action |
|---|---|
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> or arrows | Move |
| <kbd>J</kbd> | Attack |
| <kbd>Space</kbd> | Jump |
| <kbd>Tab</kbd> | Swap between the keyboard and the scripted agent |

The agent walks to the pressure pad and attacks whenever it is in range. Movement goes through the surface too, so the agent steers without ever touching the mover. Drive each scene with its agent and they walk to the same place and attack at the same rate.

### The overlay

Both scenes carry `InputSurfacePanel`, a corner ribbon showing what crosses the surface, step by step:

```
TICK 4127                              [J] [SPC] [BOT]
Attack   ▓░░▓░░░░░░▓░░░░░░░░░░░░░░░░░░░░░░░   +2
Jump     ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    —

57 fps · 50 Hz · 1.1 steps/frame
FPS  max 144 60 30 12    SCALE  1 .25 .05
```

**The lanes are a rolling history, not a live value.** At 50 Hz a frame-exact intent is armed for twenty milliseconds, so a readout of one blinks past faster than anyone can read. A press shows amber, the wait after it dim amber, the consume teal, and an intent nobody took red, once, on the step its window lapsed. The gap between the amber mark and the teal one is the buffering window made visible.

The key caps light while <kbd>J</kbd> and <kbd>Space</kbd> are held, and **BOT** while the agent is driving.

**The frame strip is the part people underestimate.** This package's whole contract is about the ratio between render frames and fixed steps, so the rate control is how you prove frame independence. Cap to 12 and the loop really does run less often, steps pile up between frames, and the readout flips from `frames/step` to `steps/frame` in amber. Above one step per frame an `Update`-phase writer starts collapsing presses, which is why the ratio is on screen rather than the raw numbers.

Both controls are global state, `Application.targetFrameRate` with vsync forced off, and `Time.timeScale`. The overlay restores whatever it found when it is disabled, so it cannot leave the game it was inspecting capped and crawling.

## Understanding the Sample

### The surface

`DemoInputSurface` is the sample's input vocabulary, and the only thing the body reads:

```cs
public class DemoInputSurface : MonoBehaviour
{
    /// <summary>Continuous intent, a direction rather than a movement.</summary>
    public Vector2 Move;

    /// <summary>Discrete intent, buffered long enough to survive until the reader's chain window opens.</summary>
    [FixedInputWindow(14)]
    public FixedInputEvent Attack;

    /// <summary>Discrete intent, with a short window so a press made just before landing still jumps.</summary>
    [FixedInputWindow(6)]
    public FixedInputEvent Jump;

    void OnEnable() => FixedInputDiagnostics.Register(this);

    void OnDisable() => FixedInputDiagnostics.Unregister(this);
}
```

### Two writers

`DeviceInputWriter` reads the keyboard in `Update`, so its presses bind to the next step. `ScriptedBotWriter` decides in `FixedUpdate` with an earlier execution order, so its presses are consumed in the step they were set. Both call `NoteSet` beside `Set`, so if both were ever live at once the [diagnostics](../advanced-usage/diagnostics.md#two-writers-in-one-step) would name them.

```cs
// DeviceInputWriter.Update
uint tick = MyFixedTick.Current;

_surface.Move = Vector2.ClampMagnitude(RawMove, 1f);

if (_attack.WasPressedThisFrame())
{
    FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Attack), tick);
    _surface.Attack.Set(tick);
}
```

```cs
// ScriptedBotWriter.FixedUpdate, at [DefaultExecutionOrder(-10)]
// A direction, and nothing else. Turning a direction into motion is the body's job.
_surface.Move = toDestination.magnitude < 0.1f
    ? Vector2.zero
    : Vector2.ClampMagnitude(new Vector2(toDestination.x, toDestination.z), 1f);

if (_target == null || toDestination.magnitude > _attackRange)
    return;

FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Attack), tick);
_surface.Attack.Set(tick);
_nextAttackTick = tick + _attackCooldownTicks;
```

`DemoWriterSwitch` makes exactly one of them live by enabling one and disabling the other. Each writer clears `Move` when it is disabled, so the body does not keep walking in the last direction it was given.

### The reader

`DemoBody` turns intents into motion, in `FixedUpdate`. Its attack shows the shape the [peek/consume split](../advanced-usage/fixed-input-event.md#peek-and-tryconsume) exists for, two ways into one action:

```cs
// Peek, because whether the attack may start depends on a second clause.
bool wantsAttack = _surface.Attack.Peek(tick, _attackBufferTicks);

// A fresh attack from standing, or a chain out of the tail of the current one.
bool canStart = !_attacking;
bool canChain = _attacking && !Before(tick, unchecked(_attackEndsAtTick - _attackChainTicks));

if (wantsAttack && (canStart || canChain))
{
    // Only now, at the point of acting, is the press consumed.
    _surface.Attack.TryConsume(tick, _attackBufferTicks);
    _attacking = true;
    _attackEndsAtTick = tick + _attackDurationTicks;
}
```

The attack buffer is 14 steps, about 280 ms at 50 Hz, long enough for a press made early in an attack to survive until the chain window opens. Jump is buffered for 6 steps, so a press made just before landing still jumps.

### Presentation

The animation is presentation only. It reads the body and never feeds back into it, which is why the clips' root motion is removed rather than applied: the body moves in fixed steps from the input surface, identically for a player and an agent, and an Animator moving it instead would make motion depend on which clip happens to be playing.

The presentation draws the body one step behind, blended between its last two positions so it stays smooth at any frame rate, and feeds the Animator the body's measured speed. Each clip is placed in the blend at its authored ground speed, so the feet move with the ground rather than sliding across it.

### The Entities scene

The Entities scene repeats all of the above as systems: the same surface as an `IComponentData`, a keyboard writer in `InitializationSystemGroup`, a Burst-compiled agent writer and body in `FixedStepSimulationSystemGroup`, the same speed, buffer, chain window and cooldown.

It also shows the hybrid clock problem being solved rather than described. `DemoEntitiesClock` matches the two timesteps and makes the ECS tick authoritative:

```cs
FixedStepSimulationSystemGroup group = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();

if (group != null)
    group.Timestep = Time.fixedDeltaTime;

MyFixedTick.Source = new EntitiesTickSource(world);
```

Without those two lines the counts drift hundreds of steps apart within a minute, and every age the overlay prints is nonsense. See [Entities](../advanced-usage/entities.md#hybrid-projects-one-clock).

## Using the overlay in your project

`InputSurfacePanel` is part of the sample, not the package, so copy it from `Scripts/Runtime` (with `IntentTrack.cs` and the `UI` folder) to use it in a project of your own. Add it to a `UIDocument` and point it at a surface:

```cs
panel.Watch(characterInput);
panel.AddChip("J", () => attack.IsPressed());   // optional key caps
```

It finds every intent on the object by reflection and reads each field's window from its `[FixedInputWindow]`.

A runtime that steps somewhere other than the engine's fixed phase, ECS being the obvious one, must drive sampling itself:

```cs
panel.SampleOnStepClosing = false;   // then call panel.SampleNow(tick) where the step closes
```

Sampling from the wrong phase reads the surface either side of the step that changed it, which shows every intent exactly one step late.

**Its look** comes from `UI/InputSurfacePanel.uss`, assigned to its **Style Sheet** field, and every color and size there is a `--fixed-input-*` variable with a fallback. Sheets in its **Style Sheets** list restyle it by setting those variables on `:root`. In the sample, `UI/InputSurfacePanelBrand.uss` does that to give it the My Gamedev Tools brand, from the brand's UI kit that ships inside the sample. A Panel Settings theme can't set the variables itself: a theme's variables don't reach a sheet attached to the document.

## Art and license

The character and environment are taken from Unity's [3D Game Kit](https://assetstore.unity.com/packages/templates/tutorials/3d-game-kit-115747): Ellen, her locomotion, attack and jump animations, a pressure pad as the agent's target, and a ground texture.

That art is licensed under the [Unity Companion License](https://github.com/mygamedevtools/fixed-input/blob/main/Packages/com.mygamedevtools.fixed-input/Samples/InputSurfaceReference/3D%20Game%20Kit%20License.md), not MIT. It can be used in connection with Unity, which is the only place a sample runs anyway.

## Wrap-up

With this sample, you drove one character from a keyboard and from a script through the same surface, watched every intent cross it step by step, and capped the frame rate to see that nothing about the result depends on it.
Use `DemoInputSurface`, `DemoBody` and the two writers as the starting point for your own surface ✨.
