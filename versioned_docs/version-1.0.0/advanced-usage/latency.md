---
sidebar_position: 5
description: Where input latency comes from, and which parts of it you can remove.
---

# Latency

Three separate delays sit between a button going down and a reader acting on it. They have different causes, and only one of them can actually be removed. A fourth, reader gating, is the one buffering exists for.

| Delay | Cause | Removable? |
|---|---|---|
| Polling | Input is sampled, not pushed | No, only shortened |
| The binding step | `Update` runs after the frame's fixed steps | Yes, with a trade |
| No rollback | A step that ran without the intent is finished | No |
| Reader gating | The reader is not ready to act yet | Tolerated by a window |

## Polling delay

You learn about a press at the next input update, so on average you wait half a sampling interval and at worst a whole one. Sampling more often shortens it. Nothing removes it.

## The binding step

In the default setup the Input System processes events right before every `Update`, and `Update` runs *after* that frame's fixed steps. A press captured there binds to the next step, which begins on the next frame. At 60 fps that is about 17 ms; at 12 fps it is 83 ms.

This one is removable. The Input System can instead process events right before every `FixedUpdate` (**Update Mode** set to **Process Events In Fixed Update**), and then a press is refreshed and consumed in the very same step with no forward binding at all.

It is a real trade rather than a free win:

* There is no per-frame input, which UI and camera code usually want.
* Two taps falling between two steps coalesce into one, because there is only one input update per step.

### Which phase may poll a device

That follows from the setting, and only from it. In the default mode, reading device state from `FixedUpdate` logs an error in the editor and in development builds, and silently returns the dynamic value in a release player. In fixed mode the error is the other way round.

So poll wherever your project's input system updates, and stamp the tick that phase gives you. [Rule 5](./input-surface.md#5-a-writer-in-fixedupdate-runs-before-its-readers-in-the-same-step-a-writer-in-update-binds-to-the-next-step) holds either way, because it is written in terms of steps rather than phases.

| Input System update mode | Writer polls in | Stamps |
|---|---|---|
| Process Events In Dynamic Update (default) | `Update` | `T + 1`, consumed next step |
| Process Events In Fixed Update | `FixedUpdate`, before its readers | `T`, consumed this step |

## No rollback

None of this is recoverable after the fact. Once a step has run without an intent, that step is finished.

`Set` will take a tick in the past, but no reader revisits a step it has already run, and the wrapping comparison simply treats a backdated tick as older. Backdating makes an intent expire sooner, never arrive earlier.

## Reader gating

A press often arrives while the reader is not yet willing to act, mid-attack or mid-air. A [window](./fixed-input-event.md#buffering) does not reverse the wait, it tolerates it, which is why widening one feels like removing lag even though the press arrived just as late.
