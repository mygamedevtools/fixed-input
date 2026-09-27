# Fixed Input

A deterministic fixed-step tick, and a one-shot input intent bound to a single step.

```csharp
// A writer, wherever input comes from.
input.Attack.Set(MyFixedTick.Current);

// A reader, later in the step.
if (input.Attack.TryConsume(tick))
    Attack();
```

An intent is valid for exactly one fixed simulation step and expires by arithmetic. Nothing clears
it, nothing can consume it twice, and a paused or reloaded game cannot deliver a stale press.

That is the primitive. The reason it is worth having is the thing built on top of it.

## The input surface

An **input surface** is a plain type holding a project's input vocabulary:

```csharp
public class CharacterInput : MonoBehaviour
{
    public Vector2 Move;                                  // continuous
    [FixedInputWindow(5)] public FixedInputEvent Attack;  // discrete, lenient
    public FixedInputEvent Evade;                         // discrete, frame-exact
}
```

This package ships no verbs. "Attack" and "evade" belong to a game, not to a primitive, so every
project declares its own surface. What the package supplies is the type discrete intents are made
of, a clock they can agree on, and the rules that make a surface swappable.

A **writer** fills a surface. A device reader and a scripted agent are both writers, and nothing
distinguishes them. A **reader** acts on a surface and reads nothing else.

Once a project holds that line, a character driven by a keyboard and the same character driven by a
script run the identical code. Every mechanic written for a player works for an agent the day it
ships, because there is no second path to drift out of sync: no parallel agent attack, no agent-only
movement, no mechanic that quietly only works for one of them.

## The contract

Six rules. They are short, and each one is load-bearing.

**1. A reader reads only the input surface**, never a device, an input action, or a writer.

*Break it and* the reader can no longer be driven by anything else, which is the entire benefit
gone.

**2. Continuous intents are plain fields. Discrete intents are `FixedInputEvent`.**

*Break it and* a continuous value gets a tick it does not need, or a discrete press becomes a bool
somebody forgets to clear and it fires twice.

**3. The reader owns execution.** A writer supplies values. It never calls a movement, animation or
combat API directly.

*Break it and* a different writer driving the same reader produces different behaviour, and the
surface has stopped being a contract. This is the rule most often broken by accident, because the
code that computed a direction is right there and moving the character from it is one line.

**4. Exactly one writer per surface per frame.**

*Break it and* two writers overwrite each other and one of the presses is silently lost. The usual
cause is a scripted writer driving a character whose device writer is still enabled.

**5. A writer in `FixedUpdate` runs before its readers in the same step. A writer in `Update` binds
to the next step.**

*Break it and* every intent expires unconsumed, because the reader tests a step the writer has not
stamped yet. This is the one rule the package cannot enforce, only report.

**6. Conditions call `Peek`. The code that acts calls `TryConsume`.**

*Break it and* consider two transitions out of one state, one testing attack plus a timing window
and one testing attack alone. A condition that consumed while merely testing would eat the press
whenever its own second clause failed, and the second transition would never see it.

## The tick

> **Tick** is the index of the fixed simulation step that will consume intents set right now. It
> advances at the **end** of the fixed-update phase.
>
> An intent set at tick `T` is consumable only during step `T`, or, when a reader passes a window,
> during steps `T` through `T + window`.

Advancing at the end is what makes both writer positions correct without either knowing about the
other:

| Writer runs in | Sees | Consumed |
|---|---|---|
| `FixedUpdate` | `T`, the step it is inside | later in step `T` |
| `Update` | `T + 1`, because step `T` has closed | in step `T + 1` |

A frame rate faster than the fixed rate collapses repeated presses harmlessly. A frame rate slower
costs exactly one step of latency and loses nothing.

`PlayerLoopTickSource` is the default and installs itself, so there is no component to add, no
prefab to wire, and no script execution order to get right. It appends its increment to the end of
the fixed-update phase, which is why physics callbacks and coroutines resuming from
`WaitForFixedUpdate` observe the step they are actually in.

Nothing about it is mandatory. `ITickSource` is an interface and `MyFixedTick.Source` is settable:

```csharp
MyFixedTick.Source = myNetcodeTick;          // a clock the project already owns
MyFixedTick.Source = new ManualTickSource(); // a test
```

`MonoBehaviourTickSource` is the escape hatch for a contested player loop. It counts in
`FixedUpdate` at execution order 32000 and touches no engine internals. The trade is that it
increments inside the script phase, before physics, so a physics callback during step `T` sees
`T + 1`.

## Latency, and which parts of it you can remove

Three separate delays sit between a button going down and a reader acting on it. They have
different causes and only one of them can actually be removed.

**Polling delay.** You learn about a press at the next input update, so on average you wait half a
sampling interval and at worst a whole one. Sampling more often shortens it. Nothing removes it.

**The binding step.** In the default setup the Input System processes events right before every
`Update`, and `Update` runs *after* that frame's fixed steps. A press captured there binds to the
next step, which begins on the next frame. At 60 fps that is about 17 ms; at 12 fps it is 83 ms.

This one is removable. The Input System can instead process events right before every
`FixedUpdate`, and then a press is refreshed and consumed in the very same step with no forward
binding at all. It is a real trade rather than a free win: in that mode there is no per-frame input,
which UI and camera code usually want, and two taps falling between two steps coalesce into one
because there is only one input update per step.

**Which phase may poll a device follows from that setting, and only from it.** In the default mode,
reading device state from `FixedUpdate` logs an error in the editor and in development builds, and
silently returns the dynamic value in a release player. In fixed mode the error is the other way
round. So poll wherever your project's input system updates, and stamp the tick that phase gives
you. Rule 5 holds either way, because it is written in terms of steps rather than phases.

None of this is recoverable after the fact. There is no rollback: once a step has run without an
intent, that step is finished. `Set` will take a tick in the past, but no reader revisits a step it
has already run, and the wrapping comparison simply treats a backdated tick as older. Backdating
makes an intent expire sooner, never arrive earlier.

**Reader gating** is the fourth delay and the one buffering exists for. A press often arrives while
the reader is not yet willing to act, mid-attack or mid-air. A window does not reverse the wait, it
tolerates it, which is why widening one feels like removing lag even though the press arrived just
as late.

## Buffering

The window is an argument to each read rather than a property of the intent, so one press can be
strict for one reader and lenient for another:

```csharp
input.Attack.Peek(tick)            // frame-exact
input.Attack.Peek(tick, window: 5) // ~100 ms at 50 Hz
```

Buffering is the whole reason `TryConsume` exists. Widen a window and an intent stays visible for
several steps, so without explicit consumption a buffered attack fires once per step.

## Diagnostics

In the editor and development builds, the failures that are otherwise silent announce themselves.

Register a surface and any intent nobody consumes is reported by name:

```csharp
void OnEnable()  => FixedInputDiagnostics.Register(this);
void OnDisable() => FixedInputDiagnostics.Unregister(this);
```

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either nothing reads this field, or its reader runs before its writer in the same step.
```

A wrong execution order, a missing tick source, or a reader that forgot to read used to produce a
character that mysteriously ignored input. Now it produces that.

`[FixedInputWindow(5)]` on a field tells the check how long that field is allowed to stay armed,
since the window itself lives at the call site.

For rule 4, call `NoteSet` beside `Set` and two writers in one step are reported with both call
sites:

```csharp
FixedInputDiagnostics.NoteSet(surface, nameof(surface.Attack), tick);
surface.Attack.Set(tick);
```

All of it compiles to nothing outside development builds, and none of it lives inside
`FixedInputEvent`.

## Entities

The package has no Entities dependency. When the Entities package is present, an extra assembly
compiles itself in and adds an ECS clock:

- `FixedTickSingleton`, advanced by a system ordered last in `FixedStepSimulationSystemGroup`
- `EntitiesTickSource`, so hybrid managed code can run off the same clock through `ITickSource`

`FixedInputEvent` needs nothing ECS-specific. It is two unmanaged fields and four methods of pure
arithmetic, so it drops into an `IComponentData` and Burst compiles it.

The entity diagnostic is better than the managed one: it needs no registration at all. ECS publishes
its own type layout, so the scan finds every component holding an intent, wherever inside the
component it sits.

Two limits worth knowing:

- **Two clocks by default.** Unity's fixed timestep is 50 Hz; `FixedStepSimulationSystemGroup`
  defaults to 60 Hz, and it runs its own catch-up loop rather than the engine's fixed phase. A
  hybrid project running both has two counters that drift. Pick one as authoritative and point
  `MyFixedTick.Source` at it.
- **The entity inspector shows raw fields.** Entities keeps its inspector extension points internal,
  so there is no supported way to render the nice row there. Authoring components use the normal
  drawer and are unaffected.

## The runtime overlay

`InputSurfacePanel` is a corner ribbon showing what crosses a surface, step by step. Add it to a
`UIDocument` and point it at a surface:

```csharp
panel.Watch(characterInput);
panel.AddChip("J", () => attack.IsPressed());   // optional key caps
```

It finds every intent on the object by reflection and reads each field's window from its
`[FixedInputWindow]`.

```
TICK 4127                              [J] [SPC] [BOT]
Attack   ▓░░▓░░░░░░▓░░░░░░░░░░░░░░░░░░░░░░░   +2
Jump     ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    —

57 fps · 50 Hz · 1.1 steps/frame
FPS  max 144 60 30 12    SCALE  1 .25 .05
```

**The lanes are a rolling history, not a live value.** At 50 Hz a frame-exact intent is armed for
twenty milliseconds, so a readout of one blinks past faster than anyone can read. A press shows
amber, the wait after it dim amber, the consume teal, and an intent nobody took red, once, on the
step its window lapsed. The gap between the amber mark and the teal one is the buffering window
made visible.

**The frame strip is the part people underestimate.** This package's whole contract is about the
ratio between render frames and fixed steps, so the rate control is how you prove frame
independence rather than assume it. Cap to 12 and the loop really does run less often, steps pile
up between frames, and the readout flips from `frames/step` to `steps/frame` in amber. Above one
step per frame an `Update`-phase writer starts collapsing presses, which is the whole reason the
ratio is on screen rather than the raw numbers.

Both controls are global state, `Application.targetFrameRate` with vsync forced off, and
`Time.timeScale`. The overlay restores whatever it found when it is disabled, so it cannot leave
the game it was inspecting capped and crawling.

A runtime that steps somewhere other than the engine's fixed phase, ECS being the obvious one, must
drive sampling itself:

```csharp
panel.SampleOnStepClosing = false;   // then call panel.SampleNow(tick) where the step closes
```

Sampling from the wrong phase reads the surface either side of the step that changed it, which
shows every intent exactly one step late.

## Inspector

A `FixedInputEvent` field draws as readable state rather than an integer:

```
Attack     ●  set 3 ticks ago   (window 5)
Evade      ○  idle
Jump       ●  pending next step
```

Outside play mode it shows the stored state instead of an invented age, because no tick is running.
An intent shown as armed in edit mode means runtime state was serialized into an asset, which is
worth seeing.

## Samples

Each ships a scene. Open it and press play.

Both carry the runtime panel, so the same overlay renders a MonoBehaviour game and an ECS one.

- **Input Surface Reference** — one character, a keyboard writer and a scripted agent writer, and a
  runtime toggle on Tab. Movement goes through the surface too, so the agent steers without ever
  touching the mover.
- **Input Surface Reference (Entities)** — the same demonstration as ECS systems, deliberately down
  to the numbers: one character, the same two writers, the same Tab to swap between them, the same
  speed, buffer, chain window and cooldown. Drive each with its agent and they walk to the same
  place and attack at the same rate. Requires the Entities package.

  Entities have no visual without the Entities Graphics package, which is a lot of apparatus for a
  sample about input, so the panel is the sample's only output. The entities create themselves from
  a system, so there is no subscene to bake.

  It also shows the hybrid clock problem being solved rather than described: `DemoEntitiesClock`
  matches the two timesteps and makes the ECS tick authoritative. Without those two lines the counts
  drift hundreds of steps apart within a minute, and every age the panel prints is nonsense.

## Requirements

Unity 6000.0 or newer. No package dependencies.
