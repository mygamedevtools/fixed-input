namespace MyGameDevTools.FixedInput
{
    /// <summary>A tick source driven by hand, for tests and deterministic replay.</summary>
    public sealed class ManualTickSource : ITickSource
    {
        public uint Tick { get; private set; }

        public ManualTickSource(uint startTick = 0) => Tick = startTick;

        /// <summary>Closes the current step and moves to the next one.</summary>
        public void Advance() => Tick++;

        /// <summary>Closes steps at once.</summary>
        public void Advance(uint steps) => Tick += steps;

        /// <summary>Jumps to an arbitrary step.</summary>
        public void SetTick(uint tick) => Tick = tick;
    }
}
