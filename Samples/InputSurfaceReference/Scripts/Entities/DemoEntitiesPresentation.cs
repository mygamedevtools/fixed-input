using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples.Entities
{
    /// <summary>Gives each entity a visible, animated character, without Entities Graphics.</summary>
    /// <remarks>
    /// Presentation only: it reads the simulation and never writes to it. It presents each body
    /// exactly the way the MonoBehaviour sample's DemoBodyAnimation does.
    /// </remarks>
    [AddComponentMenu("Fixed Input/Samples/Demo Entities Presentation")]
    public class DemoEntitiesPresentation : MonoBehaviour
    {
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        static readonly int Attacking = Animator.StringToHash("Attacking");
        static readonly int Attack = Animator.StringToHash("Attack");
        static readonly int Grounded = Animator.StringToHash("Grounded");

        const float TurnRate = 18f;

        [SerializeField]
        GameObject _characterPrefab;

        readonly Dictionary<Entity, Visual> _visuals = new Dictionary<Entity, Visual>();

        sealed class Visual
        {
            public Transform Transform;
            public Animator Animator;
            public Vector3 From, To, Velocity;
            public Quaternion Facing;
            public float StepStartedAt;
            public uint LastTick;
            public uint LastAttackEnd;
        }

        void LateUpdate()
        {
            World world = World.DefaultGameObjectInjectionWorld;

            if (world == null || !world.IsCreated || _characterPrefab == null)
                return;

            EntityManager entities = world.EntityManager;
            EntityQuery query = entities.CreateEntityQuery(
                ComponentType.ReadOnly<DemoBodyState>(),
                ComponentType.ReadOnly<LocalTransform>());

            using NativeArray<Entity> found = query.ToEntityArray(Allocator.Temp);
            uint tick = MyFixedTick.Current;

            foreach (Entity entity in found)
            {
                DemoBodyState body = entities.GetComponentData<DemoBodyState>(entity);
                LocalTransform local = entities.GetComponentData<LocalTransform>(entity);

                if (!_visuals.TryGetValue(entity, out Visual visual) || visual.Transform == null)
                {
                    GameObject instance = Instantiate(_characterPrefab, local.Position, Quaternion.identity, transform);
                    instance.name = entities.GetName(entity);
                    visual = new Visual
                    {
                        Transform = instance.transform,
                        Animator = instance.GetComponentInChildren<Animator>(),
                        From = local.Position,
                        To = local.Position,
                        Facing = Quaternion.identity,
                        LastTick = tick,
                    };
                    _visuals[entity] = visual;
                }

                if (tick != visual.LastTick)
                {
                    uint steps = unchecked(tick - visual.LastTick);
                    visual.From = visual.To;
                    visual.To = local.Position;
                    visual.Velocity = (visual.To - visual.From) / (Mathf.Max(1, steps) * Time.fixedDeltaTime);
                    visual.StepStartedAt = Time.time;
                    visual.LastTick = tick;
                }

                float3 forward = math.mul(local.Rotation, new float3(0f, 0f, 1f));
                Quaternion facing = Quaternion.Euler(0f, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 0f);
                float alpha = Mathf.Clamp01((Time.time - visual.StepStartedAt) / Time.fixedDeltaTime);
                visual.Facing = Quaternion.Slerp(visual.Facing, facing, 1f - Mathf.Exp(-TurnRate * Time.deltaTime));
                visual.Transform.SetPositionAndRotation(Vector3.Lerp(visual.From, visual.To, alpha), visual.Facing);

                if (visual.Animator == null)
                    continue;

                bool grounded = local.Position.y <= 0.001f && body.VerticalSpeed <= 0f;

                visual.Animator.SetFloat(Speed, new Vector2(visual.Velocity.x, visual.Velocity.z).magnitude, 0.08f, Time.deltaTime);
                visual.Animator.SetFloat(VerticalSpeed, visual.Velocity.y);
                visual.Animator.SetBool(Attacking, body.Attacking);
                visual.Animator.SetBool(Grounded, grounded);

                // A new end tick means a new attack started, including a chain out of the last one.
                if (body.Attacking && body.AttackEndsAtTick != visual.LastAttackEnd)
                {
                    visual.LastAttackEnd = body.AttackEndsAtTick;
                    visual.Animator.SetTrigger(Attack);
                }
            }
        }
    }
}
