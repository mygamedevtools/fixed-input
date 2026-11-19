---
sidebar_position: 1
---

# Introdução

**My Fixed Tick** é um pacote para Unity que oferece um **tick determinístico de passo fixo** e uma **intenção de input de disparo único** ligada a um único passo. Uma intenção vale por exatamente um passo fixo de simulação e expira por aritmética: nada a limpa, nada consegue consumi-la duas vezes, e um jogo pausado ou recarregado não consegue entregar um aperto obsoleto.

O que importa é o que isso torna possível. Coloque o vocabulário de input de um projeto em um tipo simples, faça todo reader ler apenas isso, e um personagem controlado por um teclado e o mesmo personagem controlado por um script rodam exatamente o mesmo código. Toda mecânica escrita para um jogador funciona para um agente no dia em que é lançada.

## Principais Funcionalidades {/* #key-features */}

* **Intenções de Input de Disparo Único**: `FixedInputEvent` vale por um passo fixo, com a separação entre peek e consume e janelas de buffering por leitura. São dois campos unmanaged, então funciona como `IComponentData` e em código compilado com Burst.
* **Um Tick Que Se Instala Sozinho**: A fonte padrão se instala no `PlayerLoop` ao fim da fase fixa, então não precisa de objeto na cena nem de ordem de execução de scripts.
* **Relógios Substituíveis**: `ITickSource` é uma interface, então um projeto pode rodar a partir de um relógio que ele já tem.
* **Diagnósticos Que Dão Nome Ao Problema**: O editor e os builds de desenvolvimento reportam uma intenção que ninguém consumiu e dois writers que disputaram o mesmo campo.
* **Suporte Opcional a Entities**: Compilado apenas quando o pacote Entities está presente.

## Instalação {/* #installation */}

Para começar a usar o My Fixed Tick, você pode instalá-lo de várias maneiras:

* [OpenUPM](./getting-started/installation.mdx#openupm)
* [Instalar pelo Git](./getting-started/installation.mdx#git)
* [Instalar por Tarball](./getting-started/installation.mdx#tarball)
* [Unity Package](./getting-started/installation.mdx#unitypackage)

## Início Rápido {/* #quick-start */}

Declare uma superfície de input, com um campo por intenção:

```cs
using MyGameDevTools.FixedInput;
// [...]

public class CharacterInput : MonoBehaviour
{
    public Vector2 Move;                                  // contínua
    [FixedInputWindow(5)] public FixedInputEvent Attack;  // discreta, tolerante
    public FixedInputEvent Evade;                         // discreta, exata no frame
}
```

Um writer define uma intenção para o tick atual, venha o input de onde vier:

```cs
input.Attack.Set(MyFixedTick.Current);
```

Um reader a consome no passo fixo para o qual ela foi definida:

```cs
void FixedUpdate()
{
    uint tick = MyFixedTick.Current;

    if (input.Attack.TryConsume(tick, window: 5))
        Attack();
}
```

Isso substitui o padrão de sempre:

```cs
bool _attackPressed;

void Update()
{
    if (Keyboard.current.jKey.wasPressedThisFrame)
        _attackPressed = true;
}

void FixedUpdate()
{
    if (_attackPressed)
    {
        _attackPressed = false;
        Attack();
    }
}
```

Essa flag continua ativa até que algo a limpe, então um jogo pausado ou recarregado entrega um aperto obsoleto, um segundo reader nunca o vê, e tudo depende de uma ordem de execução de scripts que ninguém documentou.

:::info
Continue com o [Guia Básico](./getting-started/basic-usage.md) para um writer e um reader completos, ou leia [A Superfície de Input](./advanced-usage/input-surface.md) para as seis regras que tornam uma superfície substituível.
:::
