using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Turns intents into motion.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Body")]
    [RequireComponent(typeof(DemoInputSurface))]
    public class DemoBody : MonoBehaviour
    {
        [SerializeField]
        float _speed = 4f;

        [SerializeField]
        float _jumpSpeed = 6f;

        [SerializeField]
        float _gravity = -18f;

        [SerializeField]
        [Tooltip("How long a press stays usable, in steps. Must be long enough to survive until the chain window opens, or a press during an attack can never be used. Fourteen at 50 Hz is about 280 ms.")]
        uint _attackBufferTicks = 14;

        [SerializeField]
        [Tooltip("Steps an attack takes to finish.")]
        uint _attackDurationTicks = 20;

        [SerializeField]
        [Tooltip("Steps before an attack ends during which a press chains straight into the next one.")]
        uint _attackChainTicks = 10;

        [SerializeField]
        [Tooltip("How long a jump press stays usable, in steps. Without this, a press made in mid-air is simply thrown away.")]
        uint _jumpBufferTicks = 6;

        DemoInputSurface _surface;
        float _verticalSpeed;
        uint _attackEndsAtTick;
        bool _attacking;

        /// <summary>Whether an attack is currently playing, for a sample HUD to read.</summary>
        public bool IsAttacking => _attacking;

        /// <summary>Steps remaining in the current attack, for a readout.</summary>
        public uint AttackStepsLeft
        {
            get
            {
                if (!_attacking)
                    return 0;

                uint remaining = unchecked(_attackEndsAtTick - MyFixedTick.Current);
                return remaining > uint.MaxValue / 2 ? 0 : remaining;
            }
        }

        void Awake() => _surface = GetComponent<DemoInputSurface>();

        /// <summary>Whether one tick comes before another, across the wrap.</summary>
        static bool Before(uint tick, uint other) => unchecked(tick - other) > uint.MaxValue / 2;

        void FixedUpdate()
        {
            uint tick = MyFixedTick.Current;
            float step = Time.fixedDeltaTime;

            if (_attacking && !Before(tick, _attackEndsAtTick))
                _attacking = false;

            // Peek, because whether the attack may start depends on a second clause.
            bool wantsAttack = _surface.Attack.Peek(tick, _attackBufferTicks);

            // Two ways in, and the shape the peek-then-consume split exists for: a fresh attack from
            // standing, or a chain out of the tail of the current one.
            bool canStart = !_attacking;
            bool canChain = _attacking && !Before(tick, unchecked(_attackEndsAtTick - _attackChainTicks));

            if (wantsAttack && (canStart || canChain))
            {
                // Only now, at the point of acting, is the press consumed.
                _surface.Attack.TryConsume(tick, _attackBufferTicks);
                _attacking = true;
                _attackEndsAtTick = tick + _attackDurationTicks;
            }

            Vector3 move = new Vector3(_surface.Move.x, 0f, _surface.Move.y);

            // Rooted while attacking, which is exactly the kind of mechanic that has to behave the
            // same whether a person or a script is driving.
            if (_attacking)
                move = Vector3.zero;
            else if (move.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(move);

            bool grounded = transform.position.y <= 0.001f && _verticalSpeed <= 0f;

            if (grounded)
            {
                _verticalSpeed = 0f;

                if (_surface.Jump.TryConsume(tick, _jumpBufferTicks))
                    _verticalSpeed = _jumpSpeed;
            }

            _verticalSpeed += _gravity * step;

            Vector3 position = transform.position + move * (_speed * step) + Vector3.up * (_verticalSpeed * step);

            if (position.y < 0f)
            {
                position.y = 0f;
                _verticalSpeed = 0f;
            }

            transform.position = position;
        }
    }
}
