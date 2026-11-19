using System;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>What became of an intent during one fixed step.</summary>
    public enum IntentActivity
    {
        /// <summary>Nothing happened.</summary>
        Quiet,

        /// <summary>An intent was set during this step and is still waiting.</summary>
        Set,

        /// <summary>An intent set earlier is still waiting, inside its window.</summary>
        Waiting,

        /// <summary>An intent was consumed during this step.</summary>
        Consumed,

        /// <summary>An intent was set and consumed within this same step.</summary>
        SetAndConsumed,

        /// <summary>An intent is still armed past its window, so nothing will ever take it.</summary>
        Expired
    }

    /// <summary>A rolling record of what happened to one intent, step by step.</summary>
    public sealed class IntentTrack
    {
        readonly IntentActivity[] _activity;
        readonly uint[] _tickAt;

        long _total;
        bool _seenAny;
        bool _wasArmed;
        bool _expiryReported;
        uint _lastSetTick;

        /// <summary>How many steps of history are kept.</summary>
        public int Capacity => _activity.Length;

        /// <summary>How many steps are currently held, up to the capacity.</summary>
        public int Count => (int)Math.Min(_total, Capacity);

        /// <summary>Total steps sampled since this track was created.</summary>
        public long TotalSamples => _total;

        /// <summary>The field this track follows.</summary>
        public string Label { get; }

        /// <summary>The window this field is expected to be read with.</summary>
        public uint Window { get; }

        /// <summary>The most recent consume, and how many steps late it landed.</summary>
        public (uint tick, uint setTick, long age) LastConsume { get; private set; } = (0, 0, -1);

        public IntentTrack(string label, uint window, int capacity = 90)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), "A track needs room for at least one step.");

            Label = label;
            Window = window;
            _activity = new IntentActivity[capacity];
            _tickAt = new uint[capacity];
        }

        /// <summary>The activity steps before the newest sample, where zero is the newest.</summary>
        public IntentActivity ActivityAt(int stepsBack) =>
            stepsBack < 0 || stepsBack >= Count ? IntentActivity.Quiet : _activity[SlotOf(stepsBack)];

        /// <summary>The step the newest sample belongs to.</summary>
        public uint NewestTick => _total == 0 ? 0 : _tickAt[SlotOf(0)];

        /// <summary>The step the oldest kept sample belongs to.</summary>
        public uint OldestTick => _total == 0 ? 0 : _tickAt[SlotOf(Count - 1)];

        int SlotOf(int stepsBack)
        {
            long at = (_total - 1 - stepsBack) % Capacity;
            return (int)(at < 0 ? at + Capacity : at);
        }

        /// <summary>Records the intent's state at the close of a step.</summary>
        public void Sample(in FixedInputEvent value, uint closingTick)
        {
            IntentActivity activity = Classify(value, closingTick);

            if (activity == IntentActivity.Consumed || activity == IntentActivity.SetAndConsumed)
                LastConsume = (closingTick, value.SetTick, SignedAge(closingTick, value.SetTick));

            int slot = (int)(_total % Capacity);
            _activity[slot] = activity;
            _tickAt[slot] = closingTick;
            _total++;

            _seenAny = true;
            _wasArmed = value.IsArmed;
            _lastSetTick = value.SetTick;
        }

        IntentActivity Classify(in FixedInputEvent value, uint closingTick)
        {
            // A set tick that moved proves a write happened during this step, whatever the armed
            // flag now says.
            bool written = _seenAny ? value.SetTick != _lastSetTick : value.IsArmed;

            // A fresh press is a fresh incident, so its expiry is worth reporting again.
            if (written)
                _expiryReported = false;

            if (!value.IsArmed)
            {
                if (written)
                    return IntentActivity.SetAndConsumed;

                return _wasArmed ? IntentActivity.Consumed : IntentActivity.Quiet;
            }

            uint age = unchecked(closingTick - value.SetTick);

            // An intent bound to a future step is an Update-phase writer waiting its turn, not
            // something ancient.
            if (age > uint.MaxValue / 2)
                return IntentActivity.Set;

            if (age > Window)
            {
                // Expiry happens once, on the step the window lapses.
                if (_expiryReported)
                    return IntentActivity.Quiet;

                _expiryReported = true;
                return IntentActivity.Expired;
            }

            return written ? IntentActivity.Set : IntentActivity.Waiting;
        }

        /// <summary>Steps between a set tick and the step observing it, as a signed value.</summary>
        internal static long SignedAge(uint tick, uint setTick)
        {
            long age = unchecked(tick - setTick);
            return age > uint.MaxValue / 2 ? age - 4294967296L : age;
        }

        /// <summary>Forgets all history.</summary>
        public void Clear()
        {
            _total = 0;
            _seenAny = false;
            _wasArmed = false;
            _expiryReported = false;
            _lastSetTick = 0;
            LastConsume = (0, 0, -1);
        }
    }
}
