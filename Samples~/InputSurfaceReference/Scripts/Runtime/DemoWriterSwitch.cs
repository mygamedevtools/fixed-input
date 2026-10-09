using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Swaps which writer is driving, at runtime, while the body keeps running.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Writer Switch")]
    [RequireComponent(typeof(DemoInputSurface))]
    public class DemoWriterSwitch : MonoBehaviour
    {
        [SerializeField]
        DeviceInputWriter _device;

        [SerializeField]
        ScriptedBotWriter _bot;

        [SerializeField]
        [Tooltip("Which writer is live when the scene starts.")]
        bool _startWithDevice = true;

        InputAction _toggle;

        /// <summary>Whether the keyboard is currently driving.</summary>
        public bool DeviceIsDriving { get; private set; }

        void Awake()
        {
            _device ??= GetComponent<DeviceInputWriter>();
            _bot ??= GetComponent<ScriptedBotWriter>();

            _toggle = new InputAction("ToggleWriter", InputActionType.Button, "<Keyboard>/tab");

            Use(_startWithDevice);
        }

        void OnEnable() => _toggle.Enable();

        void OnDisable() => _toggle.Disable();

        void Update()
        {
            if (_toggle.WasPressedThisFrame())
                Use(!DeviceIsDriving);
        }

        /// <summary>Makes exactly one writer live.</summary>
        public void Use(bool device)
        {
            DeviceIsDriving = device;

            if (_device != null)
                _device.enabled = device;

            if (_bot != null)
                _bot.enabled = !device;

            Debug.Log($"[FixedInput sample] Now driven by the {(device ? "keyboard" : "scripted agent")}. The body did not notice.");
        }
    }
}
