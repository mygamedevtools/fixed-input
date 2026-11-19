using MyGameDevTools.FixedInput.Entities;
using Unity.Entities;
using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>
    /// Makes the ECS clock authoritative for the whole game, which is what a hybrid project has to
    /// do before anything else works.
    /// </summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Entities Clock")]
    [DefaultExecutionOrder(-32000)]
    public class DemoEntitiesClock : MonoBehaviour
    {
        void Awake()
        {
            World world = World.DefaultGameObjectInjectionWorld;

            if (world == null || !world.IsCreated)
            {
                Debug.LogWarning($"[{nameof(DemoEntitiesClock)}] No default world yet, so the two clocks will drift apart.");
                return;
            }

            FixedStepSimulationSystemGroup group = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();

            if (group != null)
                group.Timestep = Time.fixedDeltaTime;

            MyFixedTick.Source = new EntitiesTickSource(world);
        }

        void OnDestroy() => MyFixedTick.ResetToDefault();
    }
}
