namespace MyGameDevTools.FixedInput
{
    /// <summary>A monotonic counter of fixed simulation steps.</summary>
    public interface ITickSource
    {
        /// <summary>The step that will consume intents set right now.</summary>
        uint Tick { get; }
    }
}
