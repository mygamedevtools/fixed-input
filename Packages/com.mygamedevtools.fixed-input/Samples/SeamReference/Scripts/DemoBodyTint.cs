using UnityEngine;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>Colors the character while it is attacking.</summary>
    [AddComponentMenu("Fixed Input/Samples/Demo Body Tint")]
    public class DemoBodyTint : MonoBehaviour
    {
        static readonly Color Idle = new Color(0.72f, 0.78f, 0.92f);
        static readonly Color Attacking = new Color(0.95f, 0.66f, 0.24f);

        [SerializeField]
        DemoBody _body;

        [SerializeField]
        Renderer _renderer;

        void Reset()
        {
            _body = GetComponentInParent<DemoBody>();
            _renderer = GetComponentInChildren<Renderer>();
        }

        void Update()
        {
            if (_body == null || _renderer == null)
                return;

            _renderer.material.color = _body.IsAttacking ? Attacking : Idle;
        }
    }
}
