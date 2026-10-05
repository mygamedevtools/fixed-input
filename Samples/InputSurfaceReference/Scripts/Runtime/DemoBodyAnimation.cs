using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Animates the character from the body's state, smoothing it between fixed steps.</summary>
    /// <remarks>
    /// Presentation only: it reads the body and never moves it, so the animation cannot change what
    /// the sample demonstrates. The Entities sample presents its body the same way.
    /// </remarks>
    [AddComponentMenu("Fixed Input/Samples/Demo Body Animation")]
    public class DemoBodyAnimation : MonoBehaviour
    {
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        static readonly int Attacking = Animator.StringToHash("Attacking");
        static readonly int Attack = Animator.StringToHash("Attack");
        static readonly int Grounded = Animator.StringToHash("Grounded");

        const float TurnRate = 18f;

        [SerializeField]
        DemoBody _body;

        [SerializeField]
        Animator _animator;

        Vector3 _from, _to, _velocity;
        Quaternion _facing;
        float _stepStartedAt;
        uint _lastTick;
        bool _started;
        uint _lastAttackEnd;

        void Reset()
        {
            _body = GetComponentInParent<DemoBody>();
            _animator = GetComponentInChildren<Animator>();
        }

        void LateUpdate()
        {
            if (_body == null || _animator == null)
                return;

            Transform body = _body.transform;
            Transform visual = _animator.transform;
            uint tick = MyFixedTick.Current;

            // The body moves once per fixed step. Drawing it one step behind, blended between the last
            // two, keeps it smooth at any frame rate, and the step's displacement is its real velocity.
            if (!_started || tick != _lastTick)
            {
                uint steps = _started ? unchecked(tick - _lastTick) : 1;
                if (!_started)
                    _facing = Quaternion.Euler(0f, body.eulerAngles.y, 0f);

                _from = _started ? _to : body.position;
                _to = body.position;
                _velocity = (_to - _from) / (Mathf.Max(1, steps) * Time.fixedDeltaTime);
                _stepStartedAt = Time.time;
                _lastTick = tick;
                _started = true;
            }

            float alpha = Mathf.Clamp01((Time.time - _stepStartedAt) / Time.fixedDeltaTime);
            // Kept here rather than read back, since the visual inherits the body's snapped rotation.
            _facing = Quaternion.Slerp(_facing, Quaternion.Euler(0f, body.eulerAngles.y, 0f), 1f - Mathf.Exp(-TurnRate * Time.deltaTime));
            visual.SetPositionAndRotation(Vector3.Lerp(_from, _to, alpha), _facing);

            _animator.SetFloat(Speed, new Vector2(_velocity.x, _velocity.z).magnitude, 0.08f, Time.deltaTime);
            _animator.SetFloat(VerticalSpeed, _velocity.y);
            _animator.SetBool(Attacking, _body.IsAttacking);
            _animator.SetBool(Grounded, _body.IsGrounded);

            // A new end tick means a new attack started, including a chain out of the last one.
            if (_body.IsAttacking && _body.AttackEndsAtTick != _lastAttackEnd)
            {
                _lastAttackEnd = _body.AttackEndsAtTick;
                _animator.SetTrigger(Attack);
            }
        }
    }
}
