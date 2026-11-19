using MyGameDevTools.FixedInput.Entities;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.InputSystem;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>
    /// Creates one keyboard-driven entity and one agent-driven entity, so the sample runs without a
    /// subscene to author.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DemoSpawnSystem : ISystem
    {
        /// <summary>Where the agent walks to, matching the other sample's patrol target.</summary>
        public static readonly float3 AgentTarget = new float3(0f, 0f, 4f);

        public void OnCreate(ref SystemState state)
        {
            Entity character = state.EntityManager.CreateEntity(
                typeof(DemoInput), typeof(DemoBodyState), typeof(DemoAgentDriven), typeof(LocalTransform));

            state.EntityManager.SetComponentData(character, LocalTransform.FromPosition(float3.zero));
            state.EntityManager.SetComponentData(character, new DemoBodyState { Speed = 4f });
            state.EntityManager.SetComponentData(character, new DemoAgentDriven
            {
                Target = AgentTarget,
                AttackRange = 2f,
                AttackCooldownTicks = 30
            });
            state.EntityManager.SetName(character, "Demo Character");

            // The keyboard drives first, so the agent marker starts switched off.
            state.EntityManager.SetComponentEnabled<DemoAgentDriven>(character, false);

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state) { }
    }

    /// <summary>Writer one, the keyboard.</summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DemoDeviceWriterSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<FixedTickSingleton>();

        public void OnUpdate(ref SystemState state)
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            // The tick singleton already holds the step about to run, because the count advances at
            // the end of each group update.
            uint tick = SystemAPI.GetSingleton<FixedTickSingleton>().Value;

            // WASD and the arrows, matching the other sample's bindings key for key.
            float right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f;
            float left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f;
            float up = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
            float down = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;

            float2 move = new float2(right - left, up - down);

            bool attack = keyboard.jKey.wasPressedThisFrame;
            bool jump = keyboard.spaceKey.wasPressedThisFrame;

            foreach (RefRW<DemoInput> input in
                     SystemAPI.Query<RefRW<DemoInput>>().WithDisabled<DemoAgentDriven>())
            {
                input.ValueRW.Move = math.normalizesafe(move) * math.saturate(math.length(move));

                if (attack)
                    input.ValueRW.Attack.Set(tick);

                if (jump)
                    input.ValueRW.Jump.Set(tick);
            }
        }
    }

    /// <summary>Writer two, a scripted agent filling the identical surface.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(DemoBodySystem))]
    [BurstCompile]
    public partial struct DemoAgentWriterSystem : ISystem
    {
        static float2 ClampMagnitude(float2 value, float max)
        {
            float length = math.length(value);
            return length > max ? math.normalizesafe(value) * max : value;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<FixedTickSingleton>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<FixedTickSingleton>().Value;

            foreach ((RefRW<DemoInput> input, RefRW<DemoAgentDriven> agent, RefRO<LocalTransform> transform) in
                     SystemAPI.Query<RefRW<DemoInput>, RefRW<DemoAgentDriven>, RefRO<LocalTransform>>())
            {
                float3 toTarget = agent.ValueRO.Target - transform.ValueRO.Position;
                toTarget.y = 0f;
                float distance = math.length(toTarget);

                // Clamped rather than normalized, so it eases off as it arrives instead of
                // shuffling across the target at full speed.
                input.ValueRW.Move = distance < 0.1f
                    ? float2.zero
                    : ClampMagnitude(new float2(toTarget.x, toTarget.z), 1f);

                if (distance > agent.ValueRO.AttackRange)
                    continue;

                // Unsigned comparison, so the cooldown survives the tick wrap.
                if (unchecked(tick - agent.ValueRO.NextAttackTick) > uint.MaxValue / 2)
                    continue;

                input.ValueRW.Attack.Set(tick);
                agent.ValueRW.NextAttackTick = tick + agent.ValueRO.AttackCooldownTicks;
            }
        }
    }

    /// <summary>The reader.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [BurstCompile]
    public partial struct DemoBodySystem : ISystem
    {
        const uint AttackBufferTicks = 14;
        const uint AttackDurationTicks = 20;
        const uint AttackChainTicks = 10;
        const uint JumpBufferTicks = 6;

        /// <summary>Whether one tick comes before another, across the wrap.</summary>
        static bool Before(uint tick, uint other) => unchecked(tick - other) > uint.MaxValue / 2;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<FixedTickSingleton>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<FixedTickSingleton>().Value;
            float step = SystemAPI.Time.DeltaTime;

            foreach ((RefRW<DemoInput> input, RefRW<DemoBodyState> body, RefRW<LocalTransform> transform) in
                     SystemAPI.Query<RefRW<DemoInput>, RefRW<DemoBodyState>, RefRW<LocalTransform>>())
            {
                if (body.ValueRO.Attacking && !Before(tick, body.ValueRO.AttackEndsAtTick))
                    body.ValueRW.Attacking = false;

                // Peek to test, consume to act.
                bool wantsAttack = input.ValueRO.Attack.Peek(tick, AttackBufferTicks);

                // Two ways in: a fresh attack from standing, or a chain out of the tail of the current one.
                bool canStart = !body.ValueRO.Attacking;
                bool canChain = body.ValueRO.Attacking &&
                                !Before(tick, unchecked(body.ValueRO.AttackEndsAtTick - AttackChainTicks));

                if (wantsAttack && (canStart || canChain))
                {
                    input.ValueRW.Attack.TryConsume(tick, AttackBufferTicks);
                    body.ValueRW.Attacking = true;
                    body.ValueRW.AttackEndsAtTick = tick + AttackDurationTicks;
                }

                float3 move = body.ValueRO.Attacking
                    ? float3.zero
                    : new float3(input.ValueRO.Move.x, 0f, input.ValueRO.Move.y);

                float3 position = transform.ValueRO.Position;
                bool grounded = position.y <= 0.001f && body.ValueRO.VerticalSpeed <= 0f;

                if (grounded)
                {
                    body.ValueRW.VerticalSpeed = 0f;

                    if (input.ValueRW.Jump.TryConsume(tick, JumpBufferTicks))
                        body.ValueRW.VerticalSpeed = 6f;
                }

                body.ValueRW.VerticalSpeed += -18f * step;

                position += move * (body.ValueRO.Speed * step);
                position.y = math.max(0f, position.y + body.ValueRO.VerticalSpeed * step);

                if (position.y <= 0f)
                    body.ValueRW.VerticalSpeed = 0f;

                transform.ValueRW.Position = position;

                if (math.lengthsq(move) > 0.0001f)
                    transform.ValueRW.Rotation = quaternion.LookRotationSafe(move, math.up());
            }
        }
    }
}
