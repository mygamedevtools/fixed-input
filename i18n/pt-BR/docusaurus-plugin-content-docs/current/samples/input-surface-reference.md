---
sidebar_position: 1
description: Aprenda com o exemplo Input Surface Reference.
---

# Input Surface Reference

Este exemplo é um personagem controlado por um teclado e por um agente scriptado através da **mesma superfície de input**. Aperte <kbd>Tab</kbd> para trocar o writer em tempo de execução e o personagem continua correndo, porque o código que o move não consegue saber qual dos dois está ativo. Um overlay mostra cada intenção passo a passo e permite limitar o frame rate e escalar o tempo, para que você possa provar a independência de frame em vez de supô-la.

Ele vem em duas cenas, uma com MonoBehaviours e uma com Entities, idênticas até nos números.

## Instalação {/* #installation */}

Importe o exemplo através do **Package Manager**.

1. Abra `Window/Package Manager`.
2. Selecione `My Fixed Tick` na lista `In Project`.
3. No painel direito, selecione a aba **Samples**.
4. Clique no botão `Import` do item `Input Surface Reference`.

Os arquivos do exemplo serão instalados em `Samples/My Fixed Tick/<version>/Input Surface Reference`.

O exemplo precisa do pacote **Input System**, e a cena de Entities também precisa do **Entities**. Veja [Compatibilidade](../getting-started/compatibility.md#samples).

## Compatibilidade com Scriptable Render Pipeline {/* #scriptable-render-pipeline-compatibility */}

Os materiais usam o shader Standard do pipeline built-in, então são renderizados no pipeline built-in do jeito que estão. Ao importar o exemplo em um projeto com um **Scriptable Render Pipeline** ativo, uma caixa de diálogo aparecerá perguntando se você deseja atualizar automaticamente os materiais do exemplo.
Ele pode atualizá-los para **URP** ou para **HDRP**.

## Testando o Exemplo {/* #playing-the-sample */}

Abra qualquer uma das cenas em `Scenes/` e entre no modo de jogo:

- **InputSurfaceReference** — a cena com MonoBehaviours.
- **InputSurfaceReferenceEntities** — a mesma demonstração como sistemas ECS. As entidades são criadas por um sistema, então não há subscene para fazer bake, e um componente de apresentação espelha cada uma em um GameObject animado, então a cena não precisa do pacote Entities Graphics.

| Tecla | Ação |
|---|---|
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> ou setas | Mover |
| <kbd>J</kbd> | Atacar |
| <kbd>Space</kbd> | Pular |
| <kbd>Tab</kbd> | Alternar entre o teclado e o agente scriptado |

O agente anda até a placa de pressão e ataca sempre que está ao alcance. O movimento também passa pela superfície, então o agente se direciona sem nunca tocar no código que move o personagem. Controle cada cena com o seu agente e eles andam até o mesmo lugar e atacam no mesmo ritmo.

### O overlay {/* #the-overlay */}

As duas cenas trazem o `InputSurfacePanel`, uma faixa no canto que mostra o que passa pela superfície, passo a passo:

```
TICK 4127                              [J] [SPC] [BOT]
Attack   ▓░░▓░░░░░░▓░░░░░░░░░░░░░░░░░░░░░░░   +2
Jump     ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    —

57 fps · 50 Hz · 1.1 steps/frame
FPS  max 144 60 30 12    SCALE  1 .25 .05
```

**As faixas são um histórico contínuo, não um valor ao vivo.** A 50 Hz, uma intenção exata no frame fica armada por vinte milissegundos, então uma leitura dela passa mais rápido do que qualquer pessoa consegue ler. Um aperto aparece em âmbar, a espera depois dele em âmbar apagado, o consumo em verde-azulado, e uma intenção que ninguém pegou em vermelho, uma vez, no passo em que a sua janela expirou. O espaço entre a marca âmbar e a verde-azulada é a janela de buffering tornada visível.

As teclas acendem enquanto <kbd>J</kbd> e <kbd>Space</kbd> estão pressionadas, e **BOT** enquanto o agente está no controle.

**A faixa de frames é a parte que as pessoas subestimam.** Todo o contrato deste pacote gira em torno da proporção entre frames renderizados e passos fixos, então o controle de taxa é como você prova a independência de frame. Limite a 12 e o loop realmente roda com menos frequência, os passos se acumulam entre os frames, e a leitura troca de `frames/step` para `steps/frame` em âmbar. Acima de um passo por frame, um writer na fase de `Update` começa a juntar apertos, e é por isso que a proporção aparece na tela em vez dos números brutos.

Os dois controles são estado global, `Application.targetFrameRate` com o vsync forçado como desligado, e `Time.timeScale`. O overlay restaura o que encontrou quando é desabilitado, então não consegue deixar o jogo que estava inspecionando limitado e se arrastando.

## Entendendo o Exemplo {/* #understanding-the-sample */}

### A superfície {/* #the-surface */}

`DemoInputSurface` é o vocabulário de input do exemplo, e a única coisa que o corpo lê:

```cs
public class DemoInputSurface : MonoBehaviour
{
    /// <summary>Intenção contínua, uma direção em vez de um movimento.</summary>
    public Vector2 Move;

    /// <summary>Intenção discreta, com buffer longo o bastante para sobreviver até a janela de encadeamento do reader abrir.</summary>
    [FixedInputWindow(14)]
    public FixedInputEvent Attack;

    /// <summary>Intenção discreta, com uma janela curta para que um aperto feito logo antes de aterrissar ainda pule.</summary>
    [FixedInputWindow(6)]
    public FixedInputEvent Jump;

    void OnEnable() => FixedInputDiagnostics.Register(this);

    void OnDisable() => FixedInputDiagnostics.Unregister(this);
}
```

### Dois writers {/* #two-writers */}

`DeviceInputWriter` lê o teclado no `Update`, então os seus apertos se ligam ao próximo passo. `ScriptedBotWriter` decide no `FixedUpdate` com uma ordem de execução anterior, então os seus apertos são consumidos no passo em que foram definidos. Os dois chamam `NoteSet` ao lado de `Set`, então se os dois estivessem ativos ao mesmo tempo os [diagnósticos](../advanced-usage/diagnostics.md#two-writers-in-one-step) os nomeariam.

```cs
// DeviceInputWriter.Update
uint tick = MyFixedTick.Current;

_surface.Move = Vector2.ClampMagnitude(RawMove, 1f);

if (_attack.WasPressedThisFrame())
{
    FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Attack), tick);
    _surface.Attack.Set(tick);
}
```

```cs
// ScriptedBotWriter.FixedUpdate, com [DefaultExecutionOrder(-10)]
// Uma direção, e nada mais. Transformar uma direção em movimento é trabalho do corpo.
_surface.Move = toDestination.magnitude < 0.1f
    ? Vector2.zero
    : Vector2.ClampMagnitude(new Vector2(toDestination.x, toDestination.z), 1f);

if (_target == null || toDestination.magnitude > _attackRange)
    return;

FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Attack), tick);
_surface.Attack.Set(tick);
_nextAttackTick = tick + _attackCooldownTicks;
```

`DemoWriterSwitch` deixa exatamente um deles ativo, habilitando um e desabilitando o outro. Cada writer limpa `Move` quando é desabilitado, para que o corpo não continue andando na última direção que recebeu.

### O reader {/* #the-reader */}

`DemoBody` transforma intenções em movimento, no `FixedUpdate`. O ataque dele mostra o formato para o qual a [separação entre peek e consume](../advanced-usage/fixed-input-event.md#peek-and-tryconsume) existe, dois caminhos para uma mesma ação:

```cs
// Peek, porque se o ataque pode começar depende de uma segunda cláusula.
bool wantsAttack = _surface.Attack.Peek(tick, _attackBufferTicks);

// Um ataque novo a partir do repouso, ou um encadeamento a partir do final do ataque atual.
bool canStart = !_attacking;
bool canChain = _attacking && !Before(tick, unchecked(_attackEndsAtTick - _attackChainTicks));

if (wantsAttack && (canStart || canChain))
{
    // Só agora, no momento de agir, o aperto é consumido.
    _surface.Attack.TryConsume(tick, _attackBufferTicks);
    _attacking = true;
    _attackEndsAtTick = tick + _attackDurationTicks;
}
```

O buffer de ataque é de 14 passos, cerca de 280 ms a 50 Hz, longo o bastante para que um aperto feito no início de um ataque sobreviva até a janela de encadeamento abrir. O pulo tem buffer de 6 passos, então um aperto feito logo antes de aterrissar ainda pula.

### Apresentação {/* #presentation */}

A animação é só apresentação. Ela lê o corpo e nunca o realimenta, e é por isso que o root motion dos clipes é removido em vez de aplicado: o corpo se move em passos fixos a partir da superfície de input, de forma idêntica para um jogador e para um agente, e um Animator movendo-o no lugar faria o movimento depender de qual clipe estivesse tocando.

A apresentação desenha o corpo um passo atrás, interpolando entre as suas duas últimas posições para continuar suave em qualquer frame rate, e alimenta o Animator com a velocidade medida do corpo. Cada clipe é posicionado no blend na velocidade de solo com que foi criado, então os pés acompanham o chão em vez de deslizar sobre ele.

### A cena de Entities {/* #the-entities-scene */}

A cena de Entities repete tudo acima como sistemas: a mesma superfície como um `IComponentData`, um writer de teclado no `InitializationSystemGroup`, um writer de agente e um corpo compilados com Burst no `FixedStepSimulationSystemGroup`, com a mesma velocidade, buffer, janela de encadeamento e cooldown.

Ela também mostra o problema do relógio híbrido sendo resolvido, e não apenas descrito. `DemoEntitiesClock` iguala os dois timesteps e torna o tick do ECS a referência:

```cs
FixedStepSimulationSystemGroup group = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();

if (group != null)
    group.Timestep = Time.fixedDeltaTime;

MyFixedTick.Source = new EntitiesTickSource(world);
```

Sem essas duas linhas, as contagens se afastam centenas de passos em menos de um minuto, e toda idade que o overlay mostra é um absurdo. Veja [Entities](../advanced-usage/entities.md#hybrid-projects-one-clock).

## Usando o overlay no seu projeto {/* #using-the-overlay-in-your-project */}

`InputSurfacePanel` faz parte do exemplo, não do pacote, então copie-o de `Scripts/Runtime` (junto com `IntentTrack.cs` e a pasta `UI`) para usá-lo em um projeto seu. Adicione-o a um `UIDocument` e aponte-o para uma superfície:

```cs
panel.Watch(characterInput);
panel.AddChip("J", () => attack.IsPressed());   // teclas opcionais
```

Ele encontra por reflection toda intenção no objeto e lê a janela de cada campo a partir do seu `[FixedInputWindow]`.

Um runtime que avança os passos em outro lugar que não a fase fixa da engine, sendo o ECS o caso óbvio, precisa acionar a amostragem por conta própria:

```cs
panel.SampleOnStepClosing = false;   // depois chame panel.SampleNow(tick) onde o passo fecha
```

Amostrar na fase errada lê a superfície de um lado ou do outro do passo que a alterou, o que mostra toda intenção exatamente um passo atrasada.

**A aparência dele** vem de `UI/InputSurfacePanel.uss`, atribuído ao seu campo **Style Sheet**, e toda cor e tamanho ali é uma variável `--fixed-input-*` com um valor de fallback. Folhas de estilo na sua lista **Style Sheets** o reestilizam definindo essas variáveis no `:root`. No exemplo, `UI/InputSurfacePanelBrand.uss` faz isso para dar a ele a marca My Gamedev Tools, a partir do UI kit da marca que vem dentro do exemplo. Um tema de Panel Settings não consegue definir as variáveis por conta própria: as variáveis de um tema não chegam a uma folha de estilo anexada ao documento.

## Arte e licença {/* #art-and-license */}

O personagem e o ambiente vêm do [3D Game Kit](https://assetstore.unity.com/packages/templates/tutorials/3d-game-kit-115747) da Unity: a Ellen, as animações de locomoção, ataque e pulo dela, uma placa de pressão como alvo do agente e uma textura de chão.

Essa arte é licenciada sob a [Unity Companion License](https://github.com/mygamedevtools/fixed-input/blob/main/Packages/com.mygamedevtools.fixed-input/Samples/InputSurfaceReference/3D%20Game%20Kit%20License.md), não sob a MIT. Ela pode ser usada em conexão com o Unity, que é o único lugar onde um exemplo roda de qualquer forma.

## Conclusão {/* #wrap-up */}

Com este exemplo, você controlou um personagem a partir de um teclado e de um script através da mesma superfície, viu cada intenção passar por ela passo a passo, e limitou o frame rate para ver que nada no resultado depende dele.
Use `DemoInputSurface`, `DemoBody` e os dois writers como ponto de partida para a sua própria superfície ✨.
