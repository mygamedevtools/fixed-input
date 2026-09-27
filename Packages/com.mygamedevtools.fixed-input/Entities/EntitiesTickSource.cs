using Unity.Entities;

namespace MyGameDevTools.FixedInput.Entities
{
    /// <summary>
    /// Reports the ECS tick through ITickSource, so hybrid code runs off the same clock as
    /// the simulation.
    /// </summary>
    public sealed class EntitiesTickSource : ITickSource
    {
        readonly World _world;

        public EntitiesTickSource(World world) => _world = world;

        /// <summary>Builds a source over the default world.</summary>
        public EntitiesTickSource() : this(null) { }

        public uint Tick
        {
            get
            {
                World world = _world ?? World.DefaultGameObjectInjectionWorld;

                if (world == null || !world.IsCreated)
                    return 0;

                EntityQuery query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FixedTickSingleton>());

                // Before the initialization system has run there is no singleton yet, which is a
                // normal state during the first frames rather than an error.
                return query.CalculateEntityCount() == 0 ? 0u : query.GetSingleton<FixedTickSingleton>().Value;
            }
        }
    }
}
