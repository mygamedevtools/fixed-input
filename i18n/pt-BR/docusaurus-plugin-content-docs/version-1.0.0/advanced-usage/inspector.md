---
sidebar_position: 8
description: Como um campo FixedInputEvent é desenhado no Inspector.
---

# Inspector

Um campo `FixedInputEvent` é desenhado como um estado legível, em vez de um inteiro:

```
Attack     ●  set 3 ticks ago   (window 5)
Evade      ○  idle
Jump       ●  pending next step
```

| Estado | Significado |
|---|---|
| `○  idle` | Não armada: nunca definida, consumida ou limpa. |
| `●  set this tick` | Armada para o passo atual. |
| `●  set N ticks ago` | Armada, e ainda dentro da janela do campo. |
| `○  lapsed, set N ticks ago` | Armada, mas além da sua janela, então nenhum reader vai vê-la. |
| `●  pending next step` | Definida para um passo futuro, normalmente por um writer no `Update`. |

A janela mostrada é o [`[FixedInputWindow]`](./diagnostics.md#declaring-a-fields-window) do campo. A linha é atualizada dez vezes por segundo no modo de jogo e para de atualizar fora dele, então um inspector mostrando intenções nunca mantém o editor acordado quando está ocioso.

## Fora do modo de jogo {/* #outside-play-mode */}

Fora do modo de jogo a linha mostra o estado armazenado, `●  armed @ tick N`, em vez de uma idade inventada, porque nenhum tick está rodando. Uma intenção mostrada como armada no modo de edição significa que estado de runtime foi serializado em um asset, o que vale a pena ver.

## Nas suas próprias ferramentas {/* #in-your-own-tools */}

A linha é um elemento de UI Toolkit, `FixedInputEventView`, no assembly `MyGameDevTools.FixedInput.Editor`. Use-o em um inspector customizado ou em uma janela de editor para mostrar uma intenção do mesmo jeito:

```cs
var view = new FixedInputEventView("Attack");
view.Refresh(armed: intent.IsArmed, setTick: intent.SetTick, currentTick: MyFixedTick.Current, live: EditorApplication.isPlaying, window: 5);
```

O inspector de entidades é a exceção: o Entities mantém os seus pontos de extensão internos, então ele mostra os campos crus. Veja [Entities](./entities.md#inspector).
