---
sidebar_position: 2
description: Versões do Unity e pacotes compatíveis com o My Fixed Tick.
---

# Compatibilidade

## Versão do Unity {/* #unity-version */}

A versão mínima suportada do Unity é a `6000.0` (Unity 6).

:::info[Assinatura de Pacote]
Os tarballs das versões são assinados com a [assinatura de pacotes do UPM](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-signature.html), que o **Package Manager** verifica a partir da `6000.3` (Unity 6.3).
Versões anteriores do Unity simplesmente ignoram a assinatura.
Confira o [guia de instalação](./installation.mdx) para ver quais métodos de instalação são assinados.
:::

## Pacotes {/* #packages */}

Este pacote **não tem dependências**. Ele funciona com qualquer fonte de input, incluindo o Input Manager legado, o [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) ou o seu próprio código, porque um writer só precisa chamar `Set`.

Ele é compatível com:

* `com.unity.entities` — quando ele está instalado, um assembly extra é compilado automaticamente e adiciona um relógio ECS e um diagnóstico de entidades. Sem ele, nada extra é compilado. Veja [Entities](../advanced-usage/entities.md).

## Exemplos {/* #samples */}

O exemplo [Input Surface Reference](../samples/input-surface-reference.md) tem requisitos próprios, que o pacote não tem:

| Cena | Requer |
|---|---|
| **InputSurfaceReference** | `com.unity.inputsystem` |
| **InputSurfaceReferenceEntities** | `com.unity.inputsystem` e `com.unity.entities` |

Os scripts de Entities ficam em um assembly próprio, que só compila quando o Entities está presente, então a cena com MonoBehaviours funciona sem ele.

Os materiais do exemplo usam o shader Standard do pipeline built-in. Importe-o em um projeto **URP** ou **HDRP** e ele oferece a atualização dos materiais.
