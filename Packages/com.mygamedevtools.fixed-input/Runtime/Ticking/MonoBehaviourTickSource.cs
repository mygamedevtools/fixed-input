using UnityEngine;

namespace MyGameDevTools.FixedInput
{
    /// <summary>A tick source that counts in FixedUpdate instead of installing into the player loop.</summary>
    [AddComponentMenu("Fixed Input/MonoBehaviour Tick Source")]
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class MonoBehaviourTickSource : MonoBehaviour, ITickSource
    {
        /// <summary>Late enough that every ordinary writer and reader has already run this step.</summary>
        public const int ExecutionOrder = 32000;

        [SerializeField]
        [Tooltip("Register as the ambient MyFixedTick.Source while this component is enabled.")]
        bool _installAsAmbientSource = true;

        public uint Tick { get; private set; }

        ITickSource _previousSource;

        void OnEnable()
        {
            if (!_installAsAmbientSource)
                return;

            _previousSource = MyFixedTick.HasCustomSource ? MyFixedTick.Source : null;
            MyFixedTick.Source = this;
        }

        void OnDisable()
        {
            if (!_installAsAmbientSource)
                return;

            // Only hand the ambient slot back if nothing else claimed it in the meantime.
            if (ReferenceEquals(MyFixedTick.Source, this))
            {
                if (_previousSource != null)
                    MyFixedTick.Source = _previousSource;
                else
                    MyFixedTick.ResetToDefault();
            }

            _previousSource = null;
        }

        void FixedUpdate()
        {
            // Execution order 32000 puts this after every ordinary reader, so the step is settled.
            uint authoritative = MyFixedTick.Current;
            FixedInputDiagnostics.CheckForUnconsumed(authoritative);
            MyFixedTick.RaiseStepClosing(authoritative);

            Tick++;
        }
    }
}
