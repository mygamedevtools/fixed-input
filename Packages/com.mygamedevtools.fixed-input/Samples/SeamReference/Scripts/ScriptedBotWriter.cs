using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Fills the same input surface from a script.</summary>
    [AddComponentMenu("Fixed Input/Samples/Scripted Bot Writer")]
    [RequireComponent(typeof(DemoInputSurface))]
    [DefaultExecutionOrder(-10)]
    public class ScriptedBotWriter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("What to walk towards. Leave empty to patrol around the starting point.")]
        Transform _target;

        [SerializeField]
        [Tooltip("Attack whenever the target is at least this close.")]
        float _attackRange = 2f;

        [SerializeField]
        [Tooltip("Steps between attacks, so the bot does not press every single step.")]
        uint _attackCooldownTicks = 30;

        [SerializeField]
        [Tooltip("Radius of the patrol circle used when no target is assigned.")]
        float _patrolRadius = 4f;

        DemoInputSurface _surface;
        Vector3 _origin;
        uint _nextAttackTick;

        void Awake()
        {
            _surface = GetComponent<DemoInputSurface>();
            _origin = transform.position;
        }

        void OnDisable() => _surface.Move = Vector2.zero;

        void FixedUpdate()
        {
            uint tick = MyFixedTick.Current;

            Vector3 destination = _target != null
                ? _target.position
                : _origin + new Vector3(Mathf.Cos(tick * 0.03f), 0f, Mathf.Sin(tick * 0.03f)) * _patrolRadius;

            Vector3 toDestination = destination - transform.position;
            toDestination.y = 0f;

            // A direction, and nothing else. Turning a direction into motion is the body's job.
            _surface.Move = toDestination.magnitude < 0.1f
                ? Vector2.zero
                : Vector2.ClampMagnitude(new Vector2(toDestination.x, toDestination.z), 1f);

            if (_target == null || toDestination.magnitude > _attackRange)
                return;

            // Unsigned comparison, so this keeps working across the tick wrap.
            if (unchecked(tick - _nextAttackTick) > uint.MaxValue / 2)
                return;

            FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Attack), tick);
            _surface.Attack.Set(tick);
            _nextAttackTick = tick + _attackCooldownTicks;
        }
    }
}
