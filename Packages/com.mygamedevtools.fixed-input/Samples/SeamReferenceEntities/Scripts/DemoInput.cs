using Unity.Entities;
using Unity.Mathematics;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>The input surface, as a component.</summary>
    public struct DemoInput : IComponentData
    {
        /// <summary>Continuous intent, a direction rather than a movement.</summary>
        public float2 Move;

        /// <summary>Discrete intent, buffered long enough to survive until the reader's chain window opens.</summary>
        [FixedInputWindow(14)]
        public FixedInputEvent Attack;

        /// <summary>Discrete intent, with a short window so a press made just before landing still jumps.</summary>
        [FixedInputWindow(6)]
        public FixedInputEvent Jump;
    }

    /// <summary>The body's own state.</summary>
    public struct DemoBodyState : IComponentData
    {
        public float Speed;
        public float VerticalSpeed;
        public uint AttackEndsAtTick;
        public bool Attacking;
    }

    /// <summary>Marks the character as driven by the scripted agent rather than the keyboard.</summary>
    public struct DemoAgentDriven : IComponentData, IEnableableComponent
    {
        /// <summary>Where the agent walks to.</summary>
        public float3 Target;

        /// <summary>How close it must be before it attacks.</summary>
        public float AttackRange;

        /// <summary>Steps between attacks, so it does not press every single step.</summary>
        public uint AttackCooldownTicks;

        /// <summary>The next step an attack is allowed on.</summary>
        public uint NextAttackTick;
    }
}
