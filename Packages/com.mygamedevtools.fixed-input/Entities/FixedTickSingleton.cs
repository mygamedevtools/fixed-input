using Unity.Entities;

namespace MyGameDevTools.FixedInput.Entities
{
    /// <summary>The ECS tick, as a singleton component.</summary>
    public struct FixedTickSingleton : IComponentData
    {
        /// <summary>The step that will consume intents set right now.</summary>
        public uint Value;
    }
}
