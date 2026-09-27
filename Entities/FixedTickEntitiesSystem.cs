using Unity.Burst;
using Unity.Entities;

namespace MyGameDevTools.FixedInput.Entities
{
    /// <summary>Creates the tick singleton before anything reads it.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct FixedTickInitializationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.CreateSingleton(new FixedTickSingleton { Value = 0 }, "FixedInput Tick");
            state.RequireForUpdate<FixedTickSingleton>();
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }
    }

    /// <summary>Advances the ECS tick at the end of the fixed-step group.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct FixedTickEntitiesSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<FixedTickSingleton>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RefRW<FixedTickSingleton> tick = SystemAPI.GetSingletonRW<FixedTickSingleton>();
            tick.ValueRW.Value++;
        }
    }
}
