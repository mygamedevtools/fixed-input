using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>Swaps which writer is driving, at runtime, while the simulation keeps running.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Entities Writer Switch")]
    public class DemoEntitiesWriterSwitch : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Which writer is live when the scene starts.")]
        bool _startWithDevice = true;

        InputAction _toggle;
        bool _applied;

        /// <summary>Whether the keyboard is currently driving.</summary>
        public bool DeviceIsDriving { get; private set; } = true;

        void Awake() => _toggle = new InputAction("ToggleWriter", InputActionType.Button, "<Keyboard>/tab");

        void OnEnable() => _toggle.Enable();

        void OnDisable() => _toggle.Disable();

        void Update()
        {
            // The entity is spawned by a system, so the first apply waits until it exists.
            if (!_applied && Use(_startWithDevice))
                _applied = true;

            if (_toggle.WasPressedThisFrame())
                Use(!DeviceIsDriving);
        }

        /// <summary>Makes exactly one writer live.</summary>
        public bool Use(bool device)
        {
            World world = World.DefaultGameObjectInjectionWorld;

            if (world == null || !world.IsCreated)
                return false;

            EntityManager entities = world.EntityManager;
            EntityQuery query = entities.CreateEntityQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<DemoInput>() },
                    Options = EntityQueryOptions.IgnoreComponentEnabledState
                });

            using NativeArray<Entity> found = query.ToEntityArray(Allocator.Temp);

            if (found.Length == 0)
                return false;

            foreach (Entity entity in found)
            {
                if (entities.HasComponent<DemoAgentDriven>(entity))
                    entities.SetComponentEnabled<DemoAgentDriven>(entity, !device);
            }

            if (DeviceIsDriving != device || !_applied)
                Debug.Log($"[FixedInput sample] Now driven by the {(device ? "keyboard" : "scripted agent")}. The reader system did not notice.");

            DeviceIsDriving = device;
            return true;
        }
    }
}
