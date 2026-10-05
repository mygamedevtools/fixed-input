using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Fills the input surface from a keyboard.</summary>
    [AddComponentMenu("Fixed Input/Samples/Device Input Writer")]
    [RequireComponent(typeof(DemoInputSurface))]
    public class DeviceInputWriter : MonoBehaviour
    {
        /// <summary>The stick value this frame, before it reaches the surface.</summary>
        public Vector2 RawMove { get; private set; }

        /// <summary>Whether the attack button is held right now.</summary>
        public bool RawAttackHeld { get; private set; }

        /// <summary>Whether the jump button is held right now.</summary>
        public bool RawJumpHeld { get; private set; }

        DemoInputSurface _surface;
        InputAction _move;
        InputAction _attack;
        InputAction _jump;

        void Awake()
        {
            _surface = GetComponent<DemoInputSurface>();

            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");

            // Keyboard only.
            _attack = new InputAction("Attack", InputActionType.Button, "<Keyboard>/j");

            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        }

        void OnEnable()
        {
            _move.Enable();
            _attack.Enable();
            _jump.Enable();
        }

        void OnDisable()
        {
            _move.Disable();
            _attack.Disable();
            _jump.Disable();

            // Leaving a direction behind would keep the body walking after the writer is swapped
            // out, which is rule 4 breaking quietly rather than loudly.
            _surface.Move = Vector2.zero;
        }

        void Update()
        {
            uint tick = MyFixedTick.Current;

            RawMove = _move.ReadValue<Vector2>();
            RawAttackHeld = _attack.IsPressed();
            RawJumpHeld = _jump.IsPressed();

            _surface.Move = Vector2.ClampMagnitude(RawMove, 1f);

            if (_attack.WasPressedThisFrame())
            {
                FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Attack), tick);
                _surface.Attack.Set(tick);
            }

            if (_jump.WasPressedThisFrame())
            {
                FixedInputDiagnostics.NoteSet(_surface, nameof(DemoInputSurface.Jump), tick);
                _surface.Jump.Set(tick);
            }
        }
    }
}
