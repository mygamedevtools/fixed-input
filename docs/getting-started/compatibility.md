---
sidebar_position: 2
description: Unity versions and packages compatible with My Fixed Tick.
---

# Compatibility

## Unity Version

The minimum supported Unity Version is `6000.0` (Unity 6).

:::info[Package Signing]
Release tarballs are signed with [UPM package signing](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-signature.html), which the **Package Manager** verifies from `6000.3` (Unity 6.3) onwards.
Earlier Unity versions simply ignore the signature.
Check the [installation guide](./installation.mdx) to see which installation methods are signed.
:::

## Packages

This package has **no dependencies**. It works with any input source, including the legacy Input Manager, the [Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) or your own code, because a writer only needs to call `Set`.

It is compatible with:

* `com.unity.entities` — when it is installed, an extra assembly compiles itself in and adds an ECS clock and an entity diagnostic. Without it, nothing extra is compiled. See [Entities](../advanced-usage/entities.md).

## Samples

The [Input Surface Reference](../samples/input-surface-reference.md) sample has requirements of its own, which the package does not:

| Scene | Requires |
|---|---|
| **InputSurfaceReference** | `com.unity.inputsystem` |
| **InputSurfaceReferenceEntities** | `com.unity.inputsystem` and `com.unity.entities` |

The Entities scripts live in their own assembly, which compiles only when Entities is present, so the MonoBehaviour scene works without it.

The sample's materials use the built-in Standard shader. Import it into a **URP** or **HDRP** project and it offers to upgrade them.
