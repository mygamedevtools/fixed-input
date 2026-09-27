using MyGameDevTools.FixedInput.Entities;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>
    /// Feeds the overlay from the ECS world, mirroring what the MonoBehaviour sample does from
    /// components.
    /// </summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Entities Panel Binding")]
    public class DemoEntitiesPanelBinding : MonoBehaviour
    {
        /// <summary>
        /// A managed mirror of one entity's surface, so the panel's reflection has something to
        /// read.
        /// </summary>
        class SurfaceMirror
        {
            [FixedInputWindow(14)]
            public FixedInputEvent Attack;

            [FixedInputWindow(6)]
            public FixedInputEvent Jump;
        }

        [SerializeField]
        InputSurfacePanel _panel;

        [SerializeField]
        DemoEntitiesWriterSwitch _writerSwitch;

        readonly SurfaceMirror _surface = new SurfaceMirror();

        Entity _character = Entity.Null;

        void Start()
        {
            if (_panel == null)
            {
                enabled = false;
                return;
            }

            // The same two caps as the MonoBehaviour sample, read from the same keys, so the two
            // overlays are directly comparable.
            _panel.AddChip("J", () => Keyboard.current != null && Keyboard.current.jKey.isPressed);
            _panel.AddChip("SPC", () => Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
            _panel.AddChip("BOT", () => _writerSwitch != null && !_writerSwitch.DeviceIsDriving);

            _panel.Watch(_surface, "DemoInput");

            // The ECS step does not close in the engine's fixed phase, so the panel must not sample
            // itself.
            _panel.SampleOnStepClosing = false;
            DemoEntitiesPanelSystem.Binding = this;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(DemoEntitiesPanelSystem.Binding, this))
                DemoEntitiesPanelSystem.Binding = null;
        }

        /// <summary>
        /// Copies each entity's surface into its mirror and records a sample, called from the
        /// fixed-step group at the moment the step closes.
        /// </summary>
        public void MirrorAndSample(EntityManager entities, uint closingTick)
        {
            if (_panel == null)
                return;

            ResolveEntities(entities);
            Copy(entities, _character, _surface);

            _panel.SampleNow(closingTick);
        }

        void ResolveEntities(EntityManager entities)
        {
            if (entities.Exists(_character))
                return;

            EntityQuery query = entities.CreateEntityQuery(ComponentType.ReadOnly<DemoInput>());
            using NativeArray<Entity> found = query.ToEntityArray(Allocator.Temp);

            if (found.Length > 0)
                _character = found[0];
        }

        static void Copy(EntityManager entities, Entity entity, SurfaceMirror into)
        {
            if (!entities.Exists(entity) || !entities.HasComponent<DemoInput>(entity))
                return;

            DemoInput input = entities.GetComponentData<DemoInput>(entity);
            into.Attack = input.Attack;
            into.Jump = input.Jump;
        }

    }
}
