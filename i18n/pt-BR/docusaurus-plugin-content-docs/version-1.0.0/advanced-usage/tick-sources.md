---
sidebar_position: 4
description: As fontes de tick que o pacote traz, e como fornecer a sua.
---

# Fontes de Tick

Uma fonte de tick é qualquer coisa que conte passos fixos de simulação:

```cs
public interface ITickSource
{
    /// <summary>O passo que vai consumir as intenções definidas neste momento.</summary>
    uint Tick { get; }
}
```

`MyFixedTick.Source` guarda aquela que todo reader sem uma fonte própria usa. O pacote traz três.

| Fonte | Use para |
|---|---|
| `PlayerLoopTickSource` | O padrão. Nada a configurar. |
| `MonoBehaviourTickSource` | Um projeto cujo player loop é disputado por outros pacotes. |
| `ManualTickSource` | Testes e replay determinístico. |

## PlayerLoopTickSource {/* #playerloopticksource */}

A padrão, e ela se instala sozinha: não há componente a adicionar, nem prefab a conectar, nem ordem de execução de scripts a acertar. Quando o modo de jogo começa, ela adiciona o seu incremento ao **fim** da fase de fixed update do player loop atual, e é por isso que callbacks de física e coroutines retomando de `WaitForFixedUpdate` observam o passo em que realmente estão. Quando o modo de jogo termina, ela remove o seu incremento e deixa todos os outros sistemas intactos.

| Membro | |
|---|---|
| `Instance` | A instância compartilhada. |
| `IsInstalled` | Se o sistema dela está presente no player loop neste momento. |
| `Reinstall()` | Coloca o incremento de volta no fim da fase de fixed update. |

### Quando outro pacote substitui o player loop {/* #when-another-package-replaces-the-player-loop */}

Um pacote que chama `PlayerLoop.SetPlayerLoop` com um snapshot tirado antes de esta fonte se instalar descarta o incremento. Os passos fixos continuam rodando, o tick para, e toda intenção expira sem ser consumida.

No editor e em builds de desenvolvimento, `MyFixedTick.Current` percebe isso, confere o player loop para ter certeza e reporta uma vez:

```
[MyFixedTick] Fixed steps are running but the tick is frozen at 812. Another package almost certainly
replaced the player loop and dropped this system, ...
```

Chame `PlayerLoopTickSource.Reinstall()` depois que o outro pacote tiver se instalado, ou mude para `MonoBehaviourTickSource`, que não mexe em nada interno da engine.

## MonoBehaviourTickSource {/* #monobehaviourticksource */}

A saída de emergência para um player loop disputado. Adicione **Fixed Input/MonoBehaviour Tick Source** a um objeto na sua primeira cena. Ela conta no `FixedUpdate` com ordem de execução `32000`, então todo writer e reader comum já rodou naquele passo.

Enquanto habilitada, ela se registra como `MyFixedTick.Source` e devolve o lugar para quem estava lá antes quando é desabilitada. Desmarque **Install As Ambient Source** para mantê-la fora do `MyFixedTick` e repassá-la você mesmo.

A contrapartida é que ela incrementa dentro da fase de scripts, antes da física, então um callback de física durante o passo `T` vê `T + 1`. Ela também precisa existir na cena, e sobreviver a carregamentos de cena se o relógio também tiver que sobreviver.

## ManualTickSource {/* #manualticksource */}

Uma fonte controlada manualmente, para testes e replay determinístico:

```cs
var clock = new ManualTickSource(startTick: 100);
MyFixedTick.Source = clock;

input.Attack.Set(MyFixedTick.Current);   // tick 100
Assert.IsTrue(input.Attack.Peek(clock.Tick));

clock.Advance();                         // tick 101
Assert.IsFalse(input.Attack.Peek(clock.Tick));
Assert.IsTrue(input.Attack.Peek(clock.Tick, window: 1));
```

| Membro | |
|---|---|
| `Advance()` | Fecha o passo atual e vai para o próximo. |
| `Advance(steps)` | Fecha vários passos de uma vez. |
| `SetTick(tick)` | Pula para um passo arbitrário, por exemplo perto de `uint.MaxValue` para testar o wrap. |

Chame `MyFixedTick.ResetToDefault()` no teardown de um teste. Entrar no modo de jogo restaura a fonte de qualquer forma, então uma fonte esquecida não consegue vazar para a próxima sessão.

## Sua própria fonte {/* #your-own-source */}

Implemente `ITickSource` sobre um relógio que o projeto já tem, como um tick de netcode ou um frame de lockstep, e defina-o como a fonte ambiente:

```cs
public sealed class NetcodeTickSource : ITickSource
{
    readonly MyNetworkClock _clock;

    public NetcodeTickSource(MyNetworkClock clock) => _clock = clock;

    public uint Tick => _clock.CurrentSimulationTick;
}

MyFixedTick.Source = new NetcodeTickSource(clock);
```

Mantenha a [definição](./the-tick.md) intacta: `Tick` aponta para o passo que vai consumir as intenções definidas neste momento, e avança depois que os readers daquele passo rodaram.

:::warning
Uma fonte customizada não dispara `MyFixedTick.StepClosing` nem roda a verificação de intenções não consumidas, porque as duas coisas vivem nas fontes que o pacote traz. Chame `FixedInputDiagnostics.CheckForUnconsumed(tick)` você mesmo onde o passo do seu relógio fecha, antes de ele avançar; ferramentas que fazem amostragem no `StepClosing` precisam ser acionadas a partir do mesmo lugar.
:::
