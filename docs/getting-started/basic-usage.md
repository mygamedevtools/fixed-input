---
sidebar_position: 3
description: Build an input surface, a writer and a reader.
---

# Basic Usage

Three pieces make up every use of this package:

* An **input surface**, a plain type holding a project's input vocabulary.
* A **writer**, which fills the surface. A keyboard reader and a scripted agent are both writers.
* A **reader**, which acts on the surface and reads nothing else.

This page builds one of each, then adds a second writer.

## 1. Declare an input surface

The package ships no verbs. "Attack" and "jump" belong to a game, not to a primitive, so every project declares its own surface:

```cs
using MyGameDevTools.FixedInput;
using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 Move;                                  // continuous
    [FixedInputWindow(5)] public FixedInputEvent Attack;  // discrete, lenient
    public FixedInputEvent Jump;                          // discrete, frame-exact

    void OnEnable()  => FixedInputDiagnostics.Register(this);
    void OnDisable() => FixedInputDiagnostics.Unregister(this);
}
```

Continuous intents, like a direction, are plain fields. Discrete intents, like a press, are `FixedInputEvent`.

Registering the surface lets the [diagnostics](../advanced-usage/diagnostics.md) report any intent nobody consumes, by name. `[FixedInputWindow(5)]` tells them how long `Attack` may stay armed before they call it lost. Both compile to nothing in a release build.

## 2. Write to it

A writer reads a device and stamps each press with the current tick:

```cs
using MyGameDevTools.FixedInput;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterInput))]
public class KeyboardWriter : MonoBehaviour
{
    CharacterInput _input;

    void Awake() => _input = GetComponent<CharacterInput>();

    void OnDisable() => _input.Move = Vector2.zero;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        uint tick = MyFixedTick.Current;

        float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
        float y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
        _input.Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

        if (keyboard.jKey.wasPressedThisFrame)
            _input.Attack.Set(tick);

        if (keyboard.spaceKey.wasPressedThisFrame)
            _input.Jump.Set(tick);
    }
}
```

A writer supplies values and nothing else. It never moves the character, plays an animation or calls a combat API, even when the code that computed a direction is right there.

:::tip
This writer runs in `Update`, so `MyFixedTick.Current` already names the **next** fixed step and every press binds to it. A writer in `FixedUpdate` gets the step it is inside. Both are correct; see [The Tick](../advanced-usage/the-tick.md).
:::

## 3. Read from it

A reader runs in the fixed step and owns execution:

```cs
using MyGameDevTools.FixedInput;
using UnityEngine;

[RequireComponent(typeof(CharacterInput))]
public class CharacterBody : MonoBehaviour
{
    [SerializeField] float _speed = 4f;
    [SerializeField] uint _attackBufferTicks = 5;

    CharacterInput _input;
    bool _attacking;

    void Awake() => _input = GetComponent<CharacterInput>();

    void FixedUpdate()
    {
        uint tick = MyFixedTick.Current;

        // Peek to test, because whether the attack may start depends on a second clause.
        if (_input.Attack.Peek(tick, _attackBufferTicks) && CanAttack())
        {
            // Consume only at the point of acting.
            _input.Attack.TryConsume(tick, _attackBufferTicks);
            StartAttack();
        }

        if (IsGrounded() && _input.Jump.TryConsume(tick))
            Jump();

        Vector3 move = new Vector3(_input.Move.x, 0f, _input.Move.y);
        transform.position += move * (_speed * Time.fixedDeltaTime);
    }

    // CanAttack, StartAttack, IsGrounded and Jump are your game's own.
}
```

`Peek` tests an intent without consuming it. `TryConsume` consumes it if it is visible at this tick. The optional window keeps a press usable for that many extra steps, so a press made a moment before the attack is allowed still lands. See [Fixed Input Event](../advanced-usage/fixed-input-event.md).

There is no scene object to add for the tick itself. The default tick source installs itself into the player loop when play mode starts.

## 4. Add a second writer

Because the body reads only the surface, anything that fills the surface can drive it. A scripted agent is just another writer:

```cs
[RequireComponent(typeof(CharacterInput))]
public class AgentWriter : MonoBehaviour
{
    [SerializeField] Transform _target;
    [SerializeField] float _attackRange = 2f;
    [SerializeField] uint _attackCooldownTicks = 30;

    CharacterInput _input;
    uint _nextAttackTick;

    void Awake() => _input = GetComponent<CharacterInput>();

    void OnDisable() => _input.Move = Vector2.zero;

    void FixedUpdate()
    {
        uint tick = MyFixedTick.Current;

        Vector3 toTarget = _target.position - transform.position;
        toTarget.y = 0f;

        // A direction, and nothing else. Turning it into motion is the body's job.
        _input.Move = Vector2.ClampMagnitude(new Vector2(toTarget.x, toTarget.z), 1f);

        if (toTarget.magnitude > _attackRange)
            return;

        // Wrapping comparison, so the cooldown keeps working across the tick wrap.
        if (unchecked(tick - _nextAttackTick) > uint.MaxValue / 2)
            return;

        _input.Attack.Set(tick);
        _nextAttackTick = tick + _attackCooldownTicks;
    }
}
```

The cooldown is the agent's own pacing, the same way a person does not press every single step.

Enable exactly one writer at a time. The body runs the identical code for both, so the attack buffer, the jump rules and every mechanic added later work for the agent the day they ship.

:::warning
A writer in `FixedUpdate` must run **before** its readers in the same step, or every intent it sets expires unconsumed. Give the writer an earlier execution order, for example `[DefaultExecutionOrder(-10)]`. This is the one rule the package cannot enforce, only [report](../advanced-usage/diagnostics.md).
:::

## Next steps

* [The Input Surface](../advanced-usage/input-surface.md) lists the six rules that keep a surface swappable, and what breaks without each.
* [Input Surface Reference](../samples/input-surface-reference.md) is this page as a playable scene, with an overlay that shows every intent step by step.
