---
sidebar_position: 6
description: Reporte intenções que ninguém consumiu e writers que disputaram o mesmo campo.
---

# Diagnósticos

Uma superfície de input tem duas falhas que são silenciosas por natureza: uma intenção que ninguém consumiu, e dois writers que definem a mesma intenção em um mesmo passo. As duas costumavam produzir um personagem que misteriosamente ignorava o input. `FixedInputDiagnostics` as transforma em mensagens que dão nome ao campo.

As verificações rodam no editor e em qualquer build de player que defina `UNITY_ENABLE_CHECKS`. Tudo abaixo compila para nada em um build sem esse símbolo, e nada disso vive dentro de `FixedInputEvent`, que continua com dois campos em qualquer configuração.

:::tip
Para ter as verificações em um build de player, adicione `UNITY_ENABLE_CHECKS` em **Project Settings > Player > Scripting Define Symbols** para aquele build.
:::

## Intenções que ninguém consumiu {/* #intents-nobody-consumed */}

Registre uma superfície e qualquer intenção que continue armada depois da sua janela é reportada pelo nome:

```cs
void OnEnable()  => FixedInputDiagnostics.Register(this);
void OnDisable() => FixedInputDiagnostics.Unregister(this);
```

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either nothing reads this field, or its reader runs before its writer in the same step.
A writer in FixedUpdate must execute before its readers.
```

A verificação roda uma vez por passo, depois que todo reader do passo teve a sua chance. Ela encontra por reflection todo campo `FixedInputEvent` da superfície, incluindo os privados e os herdados, e reporta cada incidente uma vez: o próximo relatório para aquele campo espera até que ele tenha sido consumido ou limpo.

O registro guarda uma referência fraca, então uma superfície que foi coletada pelo garbage collector ou destruída sai sozinha. Uma superfície não precisa ser um `MonoBehaviour`; qualquer objeto funciona, embora um componente ganhe o seu nome e o do GameObject na mensagem, além de um contexto para selecioná-lo com um clique.

### Declarando a janela de um campo {/* #declaring-a-fields-window */}

A janela vive em cada leitura, então a verificação não consegue vê-la. Sem uma declaração, um campo é considerado exato no frame, e um aperto com buffer é reportado como perdido no passo seguinte ao que foi definido. Declare a maior janela que qualquer reader passa:

```cs
[FixedInputWindow(5)] public FixedInputEvent Attack;
```

`FixedInputDiagnostics.DefaultWindow` define a janela assumida para todo campo sem o atributo.

## Dois writers em um passo {/* #two-writers-in-one-step */}

A Regra 4 permite exatamente um writer por superfície por frame. Chame `NoteSet` ao lado de `Set` e dois writers em um mesmo passo são reportados com os dois pontos de chamada:

```cs
FixedInputDiagnostics.NoteSet(surface, nameof(surface.Attack), tick);
surface.Attack.Set(tick);
```

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set twice during tick 4120, by two different writers:
  Assets/Scripts/KeyboardWriter.cs:31
  Assets/Scripts/AgentWriter.cs:44
Exactly one writer may fill an input surface per frame. ...
```

O ponto de chamada é preenchido pelo compilador, então não há mais nada a passar.

Quando as duas definições vêm do **mesmo** lugar, vira um aviso: um writer no `Update` que roda duas vezes antes de um passo fixo, porque o frame rate supera a taxa fixa, sobrescreve o seu primeiro aperto. Mova-o para o `FixedUpdate` se todo aperto precisar contar.

## Desligando {/* #switching-it-off */}

```cs
FixedInputDiagnostics.Enabled = false;
```

`Enabled` é `true` por padrão onde quer que as verificações estejam compiladas. Entrar no modo de jogo o restaura, esquece todas as superfícies registradas e redefine `DefaultWindow` para `0`.

## Entities {/* #entities */}

O diagnóstico de entidades não precisa de registro nenhum. O ECS publica o seu próprio layout de tipos, então um sistema encontra todo componente que guarda um `FixedInputEvent`, em qualquer lugar dentro do componente, e lê `[FixedInputWindow]` desses campos. Veja [Entities](./entities.md#diagnostics).

## O player loop {/* #the-player-loop */}

Mais uma verificação vive no `MyFixedTick`: se os passos fixos estão rodando mas o tick não avança, porque outro pacote substituiu o player loop e descartou a fonte padrão, ela avisa uma vez. Veja [Fontes de Tick](./tick-sources.md#when-another-package-replaces-the-player-loop).

## API {/* #api */}

| Membro | |
|---|---|
| `Register(surface)` | Observa uma superfície em busca de intenções que ninguém consome. |
| `Unregister(surface)` | Para de observar uma superfície. |
| `NoteSet(surface, field, tick)` | Registra uma escrita, para que um segundo writer no mesmo passo possa ser nomeado. |
| `CheckForUnconsumed(tick)` | Reporta toda intenção registrada que está armada além da sua janela. As fontes de tick que o pacote traz a chamam; uma [fonte customizada](./tick-sources.md#your-own-source) também deveria. |
| `Enabled` | Se as verificações rodam. |
| `DefaultWindow` | A janela assumida para um campo sem `[FixedInputWindow]`. |
