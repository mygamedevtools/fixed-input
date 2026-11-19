---
sidebar_position: 8
description: How a FixedInputEvent field draws in the Inspector.
---

# Inspector

A `FixedInputEvent` field draws as readable state rather than an integer:

```
Attack     ●  set 3 ticks ago   (window 5)
Evade      ○  idle
Jump       ●  pending next step
```

| State | Meaning |
|---|---|
| `○  idle` | Not armed: never set, consumed, or cleared. |
| `●  set this tick` | Armed for the current step. |
| `●  set N ticks ago` | Armed, and still inside the field's window. |
| `○  lapsed, set N ticks ago` | Armed, but past its window, so no reader will see it. |
| `●  pending next step` | Set for a future step, typically by a writer in `Update`. |

The window shown is the field's [`[FixedInputWindow]`](./diagnostics.md#declaring-a-fields-window). The row refreshes ten times a second in play mode and stops refreshing outside it, so an inspector showing intents never keeps the editor awake at idle.

## Outside play mode

Outside play mode the row shows the stored state, `●  armed @ tick N`, instead of an invented age, because no tick is running. An intent shown as armed in edit mode means runtime state was serialized into an asset, which is worth seeing.

## In your own tools

The row is a UI Toolkit element, `FixedInputEventView`, in the `MyGameDevTools.FixedInput.Editor` assembly. Use it in a custom inspector or editor window to show an intent the same way:

```cs
var view = new FixedInputEventView("Attack");
view.Refresh(armed: intent.IsArmed, setTick: intent.SetTick, currentTick: MyFixedTick.Current, live: EditorApplication.isPlaying, window: 5);
```

The entity inspector is the exception: Entities keeps its extension points internal, so it shows the raw fields. See [Entities](./entities.md#inspector).
