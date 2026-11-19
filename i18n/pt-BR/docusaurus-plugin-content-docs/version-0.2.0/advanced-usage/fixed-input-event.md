---
sidebar_position: 2
description: A intenção de input de disparo único, seus métodos e janelas de buffering.
---

# Fixed Input Event

`FixedInputEvent` é uma intenção de input de disparo único ligada a um único passo fixo de simulação. É uma struct com dois campos, o tick para o qual ela foi definida e se ela está armada, então não tem alocação, nem referências, nem estado da engine.

```cs
public struct FixedInputEvent
{
    public bool IsArmed { get; }   // definida, e ainda não consumida nem limpa
    public uint SetTick { get; }   // o tick para o qual ela foi definida

    public void Set(uint tick);
    public bool Peek(uint tick, uint window = 0);
    public bool TryConsume(uint tick, uint window = 0);
    public void Clear();
}
```

| Membro | |
|---|---|
| `Set(tick)` | Arma a intenção para o passo que deve consumi-la. Defini-la de novo sobrescreve o tick. |
| `Peek(tick, window)` | Se a intenção está visível em `tick`. Nunca a altera. |
| `TryConsume(tick, window)` | Desarma a intenção e retorna `true` se ela estiver visível em `tick`; caso contrário, retorna `false` e a deixa como está. |
| `Clear()` | Desarma a intenção sem agir sobre ela. |

Como é uma struct, leia e escreva nela através do campo, não de uma cópia:

```cs
input.Attack.TryConsume(tick);   // consome o campo

var attack = input.Attack;       // uma cópia
attack.TryConsume(tick);         // consome a cópia; o campo continua armado
```

## Visibilidade {/* #visibility */}

Uma intenção definida no tick `T` fica visível durante o passo `T` ou, quando um reader passa uma janela, durante os passos `T` até `T + window`. Fora desse intervalo ela fica invisível, e expira por aritmética: nada precisa limpá-la.

A comparação é uma subtração com wrap, `tick - SetTick <= window`, o que dá a ela duas propriedades:

* Ela continua correta quando o `uint` dá a volta.
* Uma intenção definida para um passo **futuro** sofre underflow e vira uma idade enorme, então fica invisível até o passo dela chegar. É isso que torna seguro um writer na fase de `Update`, que se liga ao próximo passo.

## Peek e TryConsume {/* #peek-and-tryconsume */}

`Peek` testa. `TryConsume` age. Mantenha os dois separados sempre que uma decisão tiver mais de uma cláusula:

```cs
// Peek, porque se o ataque pode começar depende de uma segunda cláusula.
bool wantsAttack = input.Attack.Peek(tick, AttackBufferTicks);

bool canStart = !attacking;
bool canChain = attacking && InChainWindow(tick);

if (wantsAttack && (canStart || canChain))
{
    // Só agora, no momento de agir, o aperto é consumido.
    input.Attack.TryConsume(tick, AttackBufferTicks);
    StartAttack(tick);
}
```

Uma condição que consumisse enquanto só está testando comeria o aperto sempre que a sua própria segunda cláusula falhasse, e uma condição posterior nunca o veria.

## Buffering {/* #buffering */}

A janela é um argumento de cada leitura, e não uma propriedade da intenção, então um mesmo aperto pode ser rigoroso para um reader e tolerante para outro:

```cs
input.Attack.Peek(tick)            // exata no frame
input.Attack.Peek(tick, window: 5) // ~100 ms a 50 Hz
```

O buffering é o motivo de o `TryConsume` existir. Amplie uma janela e a intenção fica visível por vários passos, então, sem um consumo explícito, um ataque com buffer dispara uma vez por passo.

Uma janela existe para o **bloqueio do reader**: um aperto costuma chegar enquanto o reader ainda não está disposto a agir, no meio de um ataque ou no ar. Ela não faz o aperto chegar mais cedo, ela tolera a espera. É por isso que ampliar uma janela parece remover lag, mesmo que o aperto tenha chegado tão tarde quanto antes. Veja [Latência](./latency.md).

### Dimensionando uma janela {/* #sizing-a-window */}

Uma janela é contada em passos fixos depois daquele para o qual a intenção foi definida, então quanto tempo de espera ela tolera depende do timestep fixo:

| Janela | Tempo extra a 50 Hz | Tempo extra a 60 Hz |
|---|---|---|
| `0` | nenhum, só o próprio passo | nenhum, só o próprio passo |
| `5` | 100 ms | ~83 ms |
| `14` | 280 ms | ~233 ms |

Um aperto que precisa sobreviver até uma oportunidade posterior, como a janela de encadeamento no fim de um ataque, precisa de uma janela pelo menos tão longa quanto a espera. O [exemplo](../samples/input-surface-reference.md) aplica um buffer de 14 passos ao ataque exatamente por esse motivo.

### Declarando a janela {/* #declaring-the-window */}

Como a janela vive no ponto de chamada, os diagnósticos não conseguem vê-la. Declare no próprio campo a maior janela que qualquer reader passa, para que eles saibam por quanto tempo o campo pode ficar armado antes de considerá-lo perdido:

```cs
[FixedInputWindow(14)] public FixedInputEvent Attack;
```

## No Entities e no Burst {/* #in-entities-and-burst */}

`FixedInputEvent` não precisa de nada específico de ECS. São dois campos unmanaged e quatro métodos de pura aritmética, então ele cabe direto em um `IComponentData` e o Burst o compila. Veja [Entities](./entities.md).

## Igualdade {/* #equality */}

Duas intenções são iguais quando ambas estão ociosas, ou ambas estão armadas para o mesmo tick. O tick armazenado de uma intenção desarmada não tem significado, então todo valor ocioso é comparado e gera hash da mesma forma. `ToString()` retorna `idle` ou `armed @ tick <n>`.
