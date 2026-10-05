using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>This sample's input vocabulary, and the only thing is allowed to read.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Input Surface")]
    public class DemoInputSurface : MonoBehaviour
    {
        /// <summary>Continuous intent, a direction rather than a movement.</summary>
        public Vector2 Move;

        /// <summary>Discrete intent, buffered long enough to survive until the reader's chain window opens.</summary>
        [FixedInputWindow(14)]
        public FixedInputEvent Attack;

        /// <summary>Discrete intent, with a short window so a press made just before landing still jumps.</summary>
        [FixedInputWindow(6)]
        public FixedInputEvent Jump;

        void OnEnable() => FixedInputDiagnostics.Register(this);

        void OnDisable() => FixedInputDiagnostics.Unregister(this);
    }
}
