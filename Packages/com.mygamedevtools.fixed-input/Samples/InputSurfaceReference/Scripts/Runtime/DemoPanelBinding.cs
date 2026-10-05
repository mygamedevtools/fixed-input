using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Feeds the overlay the device state the package cannot see for itself.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Panel Binding")]
    public class DemoPanelBinding : MonoBehaviour
    {
        [SerializeField]
        InputSurfacePanel _panel;

        [SerializeField]
        DemoInputSurface _surface;

        [SerializeField]
        DemoBody _body;

        [SerializeField]
        DemoWriterSwitch _writerSwitch;

        [SerializeField]
        DeviceInputWriter _device;

        void Start()
        {
            if (_panel == null || _surface == null)
            {
                enabled = false;
                return;
            }

            // The ribbon shows one piece of device state, because which button a game calls
            // "attack" is the game's business.
            _panel.AddChip("J", () => _device != null && _device.enabled && _device.RawAttackHeld);
            _panel.AddChip("SPC", () => _device != null && _device.enabled && _device.RawJumpHeld);

            // Lit while the scripted writer holds the surface.
            _panel.AddChip("BOT", () => _writerSwitch != null && !_writerSwitch.DeviceIsDriving);

            _panel.Watch(_surface, nameof(DemoInputSurface));
        }

        void Reset()
        {
            _surface = GetComponentInParent<DemoInputSurface>();
            _body = GetComponentInParent<DemoBody>();
            _writerSwitch = GetComponentInParent<DemoWriterSwitch>();
            _device = GetComponentInParent<DeviceInputWriter>();
            _panel = FindAnyObjectByType<InputSurfacePanel>();
        }
    }
}
