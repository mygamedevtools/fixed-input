---
sidebar_position: 7
description: O relógio opcional do Entities, intenções em componentes e o diagnóstico de entidades.
---

# Entities

O pacote não depende do Entities. Quando `com.unity.entities` está presente, um assembly extra, `MyGameDevTools.FixedInput.Entities`, é compilado automaticamente e adiciona um relógio ECS:

* `FixedTickSingleton`, avançado por um sistema ordenado por último no `FixedStepSimulationSystemGroup`.
* `EntitiesTickSource`, para que código gerenciado híbrido possa rodar a partir do mesmo relógio através do `ITickSource`.

## Intenções em componentes {/* #intents-in-components */}

`FixedInputEvent` não precisa de nada específico de ECS. São dois campos unmanaged e quatro métodos de pura aritmética, então ele cabe direto em um `IComponentData` e o Burst o compila:

```cs
using MyGameDevTools.FixedInput;
using Unity.Entities;
using Unity.Mathematics;

public struct CharacterInput : IComponentData
{
    public float2 Move;
    [FixedInputWindow(14)] public FixedInputEvent Attack;
    [FixedInputWindow(6)]  public FixedInputEvent Jump;
}
```

## O tick do ECS {/* #the-ecs-tick */}

`FixedTickSingleton.Value` segue a mesma [definição](./the-tick.md) que `MyFixedTick.Current`: o passo que vai consumir as intenções definidas neste momento. Ele é criado junto com o world e avançado no fim de cada update do `FixedStepSimulationSystemGroup`, depois de todo reader.

Um reader no grupo de passo fixo lê o singleton e consome:

```cs
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct CharacterBodySystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state) => state.RequireForUpdate<FixedTickSingleton>();

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        uint tick = SystemAPI.GetSingleton<FixedTickSingleton>().Value;

        foreach (RefRW<CharacterInput> input in SystemAPI.Query<RefRW<CharacterInput>>())
        {
            if (input.ValueRW.Jump.TryConsume(tick, 6))
            {
                // pulo
            }
        }
    }
}
```

Use `RefRW` e `ValueRW` para consumir. `ValueRO` retorna uma cópia, e consumir uma cópia deixa o componente armado.

As mesmas posições de writer se aplicam, assim como com MonoBehaviours:

| Writer roda em | Vê | Consumida |
|---|---|---|
| `FixedStepSimulationSystemGroup`, ordenado antes dos seus readers | `T`, o passo dentro do qual está | mais adiante no passo `T` |
| `InitializationSystemGroup`, uma vez por frame | o próximo passo a rodar, porque a contagem avançou no fim do último update do grupo | naquele passo, na próxima vez que o grupo atualizar |

## Projetos híbridos: um relógio {/* #hybrid-projects-one-clock */}

:::warning[Dois relógios por padrão]
O timestep fixo do Unity é de 50 Hz por padrão, e o do `FixedStepSimulationSystemGroup` é de 60 Hz, e roda o seu próprio loop de recuperação em vez da fase fixa da engine. Um projeto híbrido rodando os dois tem dois contadores que se afastam um do outro.
:::

Escolha um como referência e aponte `MyFixedTick.Source` para ele. Para tornar o tick do ECS a referência, iguale os dois timesteps e instale um `EntitiesTickSource`:

```cs
[DefaultExecutionOrder(-32000)]
public class EntitiesClock : MonoBehaviour
{
    void Awake()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = Time.fixedDeltaTime;
        MyFixedTick.Source = new EntitiesTickSource(world);
    }

    void OnDestroy() => MyFixedTick.ResetToDefault();
}
```

Sem isso, as duas contagens se afastam centenas de passos em menos de um minuto. `new EntitiesTickSource()` sem um world usa o world padrão, e retorna `0` até que o singleton exista.

## Diagnósticos {/* #diagnostics */}

O diagnóstico de entidades é melhor que o gerenciado: ele não precisa de registro nenhum. O ECS publica o seu próprio layout de tipos, então a varredura encontra todo componente que guarda uma intenção, em qualquer lugar dentro do componente, incluindo structs aninhadas, e lê o `[FixedInputWindow]` de cada campo.

```
[FixedInput] 'CharacterInput.Attack' on Entity(12:1) was set at tick 4120 and never consumed
(now tick 4121, window 0).
Either no system reads this field, or its reader is ordered before its writer in the same step.
```

Ele roda no editor e em builds com `UNITY_ENABLE_CHECKS`, no fim do grupo de passo fixo logo antes de o tick avançar, e se desabilita quando nenhum componente do projeto guarda uma intenção. `FixedInputDiagnostics.Enabled` e `DefaultWindow` também se aplicam a ele.

## Inspector {/* #inspector */}

O Entities mantém internos os pontos de extensão do seu inspector, então não há um jeito suportado de renderizar a [linha do inspector](./inspector.md) no inspector de entidades, que mostra os campos crus. Componentes de authoring usam o drawer normal e não são afetados.
