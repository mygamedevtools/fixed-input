---
sidebar_position: 3
description: Monte uma superfície de input, um writer e um reader.
---

# Guia Básico

Três peças compõem todo uso deste pacote:

* Uma **superfície de input**, um tipo simples que guarda o vocabulário de input de um projeto.
* Um **writer**, que preenche a superfície. Um script que lê o teclado e um agente scriptado são ambos writers.
* Um **reader**, que age sobre a superfície e não lê mais nada.

Esta página monta um de cada e depois adiciona um segundo writer.

## 1. Declare uma superfície de input {/* #1-declare-an-input-surface */}

O pacote não traz nenhum verbo. "Atacar" e "pular" pertencem a um jogo, não a um primitivo, então cada projeto declara a sua própria superfície:

```cs
using MyGameDevTools.FixedInput;
using UnityEngine;

public class CharacterInput : MonoBehaviour
{
    public Vector2 Move;                                  // contínua
    [FixedInputWindow(5)] public FixedInputEvent Attack;  // discreta, tolerante
    public FixedInputEvent Jump;                          // discreta, exata no frame

    void OnEnable()  => FixedInputDiagnostics.Register(this);
    void OnDisable() => FixedInputDiagnostics.Unregister(this);
}
```

Intenções contínuas, como uma direção, são campos simples. Intenções discretas, como um aperto, são `FixedInputEvent`.

Registrar a superfície permite que os [diagnósticos](../advanced-usage/diagnostics.md) reportem, pelo nome, qualquer intenção que ninguém consuma. `[FixedInputWindow(5)]` diz a eles por quanto tempo `Attack` pode ficar armada antes de ser considerada perdida. Ambos compilam para nada em um build de release.

## 2. Escreva nela {/* #2-write-to-it */}

Um writer lê um dispositivo e marca cada aperto com o tick atual:

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

Um writer fornece valores e nada mais. Ele nunca move o personagem, toca uma animação ou chama uma API de combate, mesmo quando o código que calculou uma direção está logo ali.

:::tip
Este writer roda no `Update`, então `MyFixedTick.Current` já aponta para o **próximo** passo fixo e todo aperto fica ligado a ele. Um writer no `FixedUpdate` recebe o passo dentro do qual está. Os dois estão corretos; veja [O Tick](../advanced-usage/the-tick.md).
:::

## 3. Leia dela {/* #3-read-from-it */}

Um reader roda no passo fixo e é dono da execução:

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

        // Peek para testar, porque se o ataque pode começar depende de uma segunda cláusula.
        if (_input.Attack.Peek(tick, _attackBufferTicks) && CanAttack())
        {
            // Consuma apenas no momento de agir.
            _input.Attack.TryConsume(tick, _attackBufferTicks);
            StartAttack();
        }

        if (IsGrounded() && _input.Jump.TryConsume(tick))
            Jump();

        Vector3 move = new Vector3(_input.Move.x, 0f, _input.Move.y);
        transform.position += move * (_speed * Time.fixedDeltaTime);
    }

    // CanAttack, StartAttack, IsGrounded e Jump são do seu próprio jogo.
}
```

`Peek` testa uma intenção sem consumi-la. `TryConsume` a consome se ela estiver visível neste tick. A janela opcional mantém um aperto utilizável por essa quantidade de passos extras, então um aperto feito um instante antes de o ataque ser permitido ainda conta. Veja [Fixed Input Event](../advanced-usage/fixed-input-event.md).

Não há objeto na cena a adicionar para o tick em si. A fonte de tick padrão se instala no player loop quando o modo de jogo começa.

## 4. Adicione um segundo writer {/* #4-add-a-second-writer */}

Como o corpo lê apenas a superfície, qualquer coisa que preencha a superfície pode controlá-lo. Um agente scriptado é só mais um writer:

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

        // Uma direção, e nada mais. Transformá-la em movimento é trabalho do corpo.
        _input.Move = Vector2.ClampMagnitude(new Vector2(toTarget.x, toTarget.z), 1f);

        if (toTarget.magnitude > _attackRange)
            return;

        // Comparação com wrap, para que o cooldown continue funcionando quando o tick dá a volta.
        if (unchecked(tick - _nextAttackTick) > uint.MaxValue / 2)
            return;

        _input.Attack.Set(tick);
        _nextAttackTick = tick + _attackCooldownTicks;
    }
}
```

O cooldown é o ritmo próprio do agente, do mesmo jeito que uma pessoa não aperta o botão em todo passo.

Habilite exatamente um writer por vez. O corpo roda exatamente o mesmo código para os dois, então o buffer de ataque, as regras de pulo e toda mecânica adicionada depois funcionam para o agente no dia em que são lançados.

:::warning
Um writer no `FixedUpdate` precisa rodar **antes** dos seus readers no mesmo passo, ou toda intenção que ele define expira sem ser consumida. Dê ao writer uma ordem de execução anterior, por exemplo `[DefaultExecutionOrder(-10)]`. Esta é a única regra que o pacote não consegue garantir, só [reportar](../advanced-usage/diagnostics.md).
:::

## Próximos passos {/* #next-steps */}

* [A Superfície de Input](../advanced-usage/input-surface.md) lista as seis regras que mantêm uma superfície substituível, e o que quebra sem cada uma.
* [Input Surface Reference](../samples/input-surface-reference.md) é esta página como uma cena jogável, com um overlay que mostra cada intenção passo a passo.
