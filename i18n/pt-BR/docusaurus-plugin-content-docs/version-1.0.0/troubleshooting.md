---
sidebar_position: 5
---

# Soluções de Problemas

## Uma intenção é definida mas nunca consumida {/* #an-intent-is-set-but-never-consumed */}

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either nothing reads this field, or its reader runs before its writer in the same step.
A writer in FixedUpdate must execute before its readers.
```

Verifique as causas nesta ordem:

1. **Nada lê o campo.** Toda intenção discreta em uma superfície registrada precisa de um reader que chame `TryConsume`, ou `Clear` se decidir não agir.
2. **O reader roda antes do writer.** Um writer no `FixedUpdate` precisa rodar antes dos seus readers no mesmo passo. Dê a ele uma ordem de execução anterior, como `[DefaultExecutionOrder(-10)]`, ou mova-o para o `Update`, onde ele se liga ao próximo passo. Veja [O Tick](./advanced-usage/the-tick.md).
3. **O reader só faz peek.** `Peek` nunca consome. O código que age precisa chamar `TryConsume`.
4. **A janela é maior do que o campo declara.** Um reader que passa `window: 5` para um campo sem `[FixedInputWindow(5)]` é reportado no passo seguinte a cada aperto, mesmo que o aperto tenha sido usado. Declare a maior janela que qualquer reader passa. Veja [Diagnósticos](./advanced-usage/diagnostics.md#declaring-a-fields-window).
5. **O aperto legitimamente não foi usado**, por exemplo um pulo apertado no ar que expirou antes de aterrissar. Se esse é o design, ignore a mensagem ou amplie a janela.

Na versão desta mensagem para Entities, a causa é a mesma, com sistemas no lugar de componentes: um sistema reader faltando, ou um reader ordenado antes do seu writer no mesmo grupo.

## Uma intenção foi definida duas vezes em um passo {/* #an-intent-was-set-twice-in-one-step */}

```
[FixedInput] 'Attack' on CharacterInput on 'Player' was set twice during tick 4120, by two different writers:
```

Dois writers estão ativos na mesma superfície. A causa comum é um writer scriptado controlando um personagem cujo writer de dispositivo ainda está habilitado. Habilite exatamente um writer por vez.

Se a mensagem disser que as duas definições vieram **do mesmo lugar**, é um único writer no `Update` rodando duas vezes antes de um passo fixo, porque o frame rate supera a taxa fixa. O primeiro aperto é sobrescrito. Isso é inofensivo para a maioria dos jogos; mova o writer para o `FixedUpdate` se todo aperto precisar contar.

## O tick está congelado {/* #the-tick-is-frozen */}

```
[MyFixedTick] Fixed steps are running but the tick is frozen at 812. Another package almost certainly
replaced the player loop and dropped this system, ...
```

Outro pacote chamou `PlayerLoop.SetPlayerLoop` com um snapshot tirado antes de a fonte de tick padrão se instalar, e descartou o incremento dela. Toda intenção vai expirar sem ser consumida. Você pode:

* chamar `PlayerLoopTickSource.Reinstall()` depois que o outro pacote tiver se instalado, ou
* usar `MonoBehaviourTickSource`, que não mexe em nada interno da engine, ou outra [fonte de tick](./advanced-usage/tick-sources.md) que o projeto controle.

## Nenhuma fase FixedUpdate encontrada {/* #no-fixedupdate-phase-found */}

```
[PlayerLoopTickSource] No FixedUpdate phase found in the player loop, so the tick cannot advance.
```

O player loop do projeto não tem uma fase de fixed update, então a fonte padrão não tem onde se instalar. Defina `MyFixedTick.Source` como uma [fonte](./advanced-usage/tick-sources.md) acionada pelo que quer que avance a simulação.

## Idades e ticks se afastam em um projeto híbrido com Entities {/* #ages-and-ticks-drift-in-a-hybrid-entities-project */}

O timestep fixo do Unity é de 50 Hz por padrão e o do `FixedStepSimulationSystemGroup` é de 60 Hz, então um projeto híbrido tem dois contadores que se afastam centenas de passos em menos de um minuto. Torne um relógio a referência: iguale os timesteps e defina `MyFixedTick.Source = new EntitiesTickSource(world)`. Veja [Entities](./advanced-usage/entities.md#hybrid-projects-one-clock).

## `MyFixedTick.Current` é sempre 0 {/* #myfixedtickcurrent-is-always-0 */}

Fora do modo de jogo, a fonte padrão retorna `0`, porque nenhum passo fixo está rodando. No modo de jogo, um `0` que nunca muda significa uma fonte customizada que não está avançando, ou um `EntitiesTickSource` cujo world ainda não criou o singleton do tick.

## Uma intenção consumida continua armada {/* #a-consumed-intent-stays-armed */}

`FixedInputEvent` é uma struct, então consumir uma cópia deixa a original armada:

```cs
var attack = input.Attack;
attack.TryConsume(tick);        // consome a cópia

input.Attack.TryConsume(tick);  // consome o campo
```

O mesmo vale no Entities: consuma através de `RefRW<T>.ValueRW`, não de `ValueRO`.

## O personagem continua andando depois de trocar de writer {/* #the-character-keeps-moving-after-swapping-writers */}

O writer desabilitado deixou o seu último valor contínuo na superfície. Limpe os campos que um writer controla quando ele for desabilitado:

```cs
void OnDisable() => _input.Move = Vector2.zero;
```

## Ler o teclado no FixedUpdate loga um erro {/* #reading-the-keyboard-in-fixedupdate-logs-an-error */}

Com o Input System no seu update mode padrão, o estado dos dispositivos só é válido no `Update`. Faça o polling dos dispositivos na fase em que o seu Input System atualiza, e marque o tick que aquela fase te dá. Veja [Latência](./advanced-usage/latency.md#which-phase-may-poll-a-device).
