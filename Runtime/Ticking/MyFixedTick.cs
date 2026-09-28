using UnityEngine;

namespace MyGameDevTools.FixedInput
{
    /// <summary>The ambient tick source, and the one place a project changes which clock is authoritative.</summary>
    public static class MyFixedTick
    {
        static ITickSource _source;
        static bool _evictionReported;

        /// <summary>The clock every unwired reader uses.</summary>
        public static ITickSource Source
        {
            get
            {
                if (_source != null)
                    return _source;

                // Deliberately not cached, or an edit-mode read would pin the fallback into the play session.
                if (!Application.isPlaying)
                    return NullTickSource.Instance;

                return _source = PlayerLoopTickSource.Instance;
            }
            set => _source = value;
        }

        /// <summary>The step that will consume intents set right now.</summary>
        public static uint Current
        {
            get
            {
#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
                ReportEvictionOnce();
#endif
                return Source.Tick;
            }
        }

        /// <summary>Raised when a fixed step closes, carrying the step that just finished.</summary>
        public static event System.Action<uint> StepClosing;

        internal static void RaiseStepClosing(uint closingTick)
        {
            try
            {
                StepClosing?.Invoke(closingTick);
            }
            catch (System.Exception exception)
            {
                // A throwing observer must not stop the clock, or a debug overlay could freeze the
                // whole simulation it exists to observe.
                Debug.LogException(exception);
            }
        }

        /// <summary>Whether a project-supplied source is in place, as opposed to the default.</summary>
        public static bool HasCustomSource => _source != null && _source is not PlayerLoopTickSource;

        /// <summary>Drops back to the default source.</summary>
        public static void ResetToDefault()
        {
            _source = null;
            _evictionReported = false;
        }

        /// <summary>Drops every observer.</summary>
        internal static void ClearObservers() => StepClosing = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnEnteringPlayMode()
        {
            // Without this, a ManualTickSource left behind by a play-mode test survives a disabled
            // domain reload and silently becomes the next session's clock.
            ResetToDefault();
            ClearObservers();
        }

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
        static void ReportEvictionOnce()
        {
            if (_source is not PlayerLoopTickSource playerLoop)
                return;

            if (!playerLoop.DetectEviction())
            {
                _evictionReported = false;
                return;
            }

            if (_evictionReported)
                return;

            _evictionReported = true;
            Debug.LogError(
                $"[{nameof(MyFixedTick)}] Fixed steps are running but the tick is frozen at {playerLoop.Tick}. " +
                "Another package almost certainly replaced the player loop and dropped this system, which happens when " +
                "something calls PlayerLoop.SetPlayerLoop with a snapshot taken before this one installed. " +
                $"Every input intent will now expire unconsumed. Call {nameof(PlayerLoopTickSource)}.{nameof(PlayerLoopTickSource.Reinstall)}() to recover, " +
                $"or set {nameof(MyFixedTick)}.{nameof(Source)} to a clock this project controls.");
        }
#endif

        sealed class NullTickSource : ITickSource
        {
            internal static readonly NullTickSource Instance = new NullTickSource();

            public uint Tick => 0;
        }
    }
}
