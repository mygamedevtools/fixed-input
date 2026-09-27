using Unity.Collections;
using Unity.Entities;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>Samples the overlay from inside the fixed-step group, where the ECS step actually closes.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(MyGameDevTools.FixedInput.Entities.FixedTickEntitiesSystem))]
    public partial class DemoEntitiesPanelSystem : SystemBase
    {
        /// <summary>The binding to push samples into.</summary>
        public static DemoEntitiesPanelBinding Binding { get; set; }

        protected override void OnCreate() => RequireForUpdate<MyGameDevTools.FixedInput.Entities.FixedTickSingleton>();

        protected override void OnUpdate()
        {
            if (Binding == null)
                return;

            uint tick = SystemAPI.GetSingleton<MyGameDevTools.FixedInput.Entities.FixedTickSingleton>().Value;
            Binding.MirrorAndSample(EntityManager, tick);
        }
    }
}
