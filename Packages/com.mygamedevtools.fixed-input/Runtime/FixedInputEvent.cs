using System;
using UnityEngine;

namespace MyGameDevTools.FixedInput
{
    /// <summary>A one-shot input intent bound to a single fixed simulation step.</summary>
    [Serializable]
    public struct FixedInputEvent : IEquatable<FixedInputEvent>
    {
        [SerializeField]
        uint _setTick;
        [SerializeField]
        bool _armed;

        /// <summary>Whether an intent has been set and not yet consumed or cleared.</summary>
        public readonly bool IsArmed => _armed;

        /// <summary>The tick this intent was set for.</summary>
        public readonly uint SetTick => _setTick;

        /// <summary>Arms the intent for the step that should consume it.</summary>
        public void Set(uint tick)
        {
            _setTick = tick;
            _armed = true;
        }

        /// <summary>Tests the intent without consuming it.</summary>
        public readonly bool Peek(uint tick, uint window = 0)
        {
            // Wrapping subtraction: correct across the uint wrap, and a future _setTick underflows
            // to a huge value, so a pending intent is invisible to a reader before its step.
            return _armed && unchecked(tick - _setTick) <= window;
        }

        /// <summary>Consumes the intent if it is visible at this tick. Use at the point of acting.</summary>
        public bool TryConsume(uint tick, uint window = 0)
        {
            if (!Peek(tick, window))
                return false;

            _armed = false;
            return true;
        }

        /// <summary>Disarms the intent without acting on it.</summary>
        public void Clear() => _armed = false;

        public readonly bool Equals(FixedInputEvent other) => _armed == other._armed && (!_armed || _setTick == other._setTick);

        public readonly override bool Equals(object obj) => obj is FixedInputEvent other && Equals(other);

        // A disarmed intent's _setTick is meaningless, so every disarmed value hashes alike.
        public readonly override int GetHashCode() => _armed ? HashCode.Combine(_setTick, true) : 0;

        public readonly override string ToString() => _armed ? $"armed @ tick {_setTick}" : "idle";
    }
}
