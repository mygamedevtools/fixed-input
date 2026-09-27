using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>Gives the entities something you can see, without pulling in Entities Graphics.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Entities Presentation")]
    public class DemoEntitiesPresentation : MonoBehaviour
    {
        // Kept in step with DemoBodyTint in the MonoBehaviour sample.
        static readonly Color Idle = new Color(0.72f, 0.78f, 0.92f);
        static readonly Color Attacking = new Color(0.95f, 0.66f, 0.24f);

        readonly Dictionary<Entity, Transform> _visuals = new Dictionary<Entity, Transform>();

        void LateUpdate()
        {
            World world = World.DefaultGameObjectInjectionWorld;

            if (world == null || !world.IsCreated)
                return;

            EntityManager entities = world.EntityManager;
            EntityQuery query = entities.CreateEntityQuery(
                ComponentType.ReadOnly<DemoInput>(),
                ComponentType.ReadOnly<DemoBodyState>(),
                ComponentType.ReadOnly<LocalTransform>());

            using NativeArray<Entity> found = query.ToEntityArray(Allocator.Temp);

            foreach (Entity entity in found)
            {
                if (!_visuals.TryGetValue(entity, out Transform visual) || visual == null)
                {
                    visual = CreateVisual(entities.GetName(entity));
                    _visuals[entity] = visual;
                }

                LocalTransform local = entities.GetComponentData<LocalTransform>(entity);
                DemoBodyState body = entities.GetComponentData<DemoBodyState>(entity);
                // Yaw only.
                Unity.Mathematics.float3 forward = Unity.Mathematics.math.mul(local.Rotation, new Unity.Mathematics.float3(0f, 0f, 1f));
                float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

                visual.SetPositionAndRotation(
                    local.Position + new Unity.Mathematics.float3(0f, 1f, 0f),
                    Quaternion.Euler(0f, yaw, 0f));

                // The only feedback that an attack is happening, since the body roots in place
                // while it plays and a still capsule otherwise looks like nothing at all.
                Renderer renderer = visual.GetComponent<Renderer>();
                renderer.material.color = body.Attacking ? Attacking : Idle;
            }
        }

        Transform CreateVisual(string label)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = string.IsNullOrEmpty(label) ? "Entity (presentation)" : label + " (presentation)";
            body.transform.SetParent(transform, false);
            Destroy(body.GetComponent<Collider>());

            GameObject facing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            facing.name = "Facing";
            facing.transform.SetParent(body.transform, false);
            facing.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            facing.transform.localScale = new Vector3(0.25f, 0.25f, 0.4f);
            Destroy(facing.GetComponent<Collider>());

            return body.transform;
        }
    }
}
