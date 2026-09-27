using UnityEngine;
using UnityEngine.UIElements;

namespace MyGameDevTools.FixedInput.Editor
{
    /// <summary>The inspector row for a FixedInputEvent, as one reusable element.</summary>
    public sealed class FixedInputEventView : VisualElement
    {
        /// <summary>
        /// A set tick further ahead than this is read as a future step rather than a very old one,
        /// which is how the wrapping subtraction distinguishes the two.
        /// </summary>
        const uint FutureThreshold = uint.MaxValue / 2;

        readonly Label _label;
        readonly Label _state;

        string _lastText;

        public FixedInputEventView(string label)
        {
            style.flexDirection = FlexDirection.Row;
            style.flexGrow = 1;

            _label = new Label(label);
            _label.AddToClassList("unity-base-field__label");
            _label.style.flexShrink = 0;

            _state = new Label
            {
                style =
                {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            Add(_label);
            Add(_state);
        }

        public void SetLabel(string label) => _label.text = label;

        /// <summary>The rendered state, as the inspector shows it.</summary>
        public string StateText => _state.text;

        /// <summary>Renders the intent's state.</summary>
        public void Refresh(bool armed, uint setTick, uint currentTick, bool live, uint window)
        {
            string text;

            if (!armed)
            {
                text = "○  idle";
            }
            else if (!live)
            {
                // Worth surfacing: an armed intent in edit mode means runtime state was serialized by accident.
                text = $"●  armed @ tick {setTick}";
            }
            else
            {
                uint age = unchecked(currentTick - setTick);

                if (age > FutureThreshold)
                    text = "●  pending next step";
                else if (age == 0)
                    text = "●  set this tick";
                else if (age <= window)
                    text = age == 1 ? "●  set 1 tick ago" : $"●  set {age} ticks ago";
                else
                    text = age == 1 ? "○  lapsed, set 1 tick ago" : $"○  lapsed, set {age} ticks ago";
            }

            if (window > 0 && armed)
                text += $"   (window {window})";

            // Assigning the same string still dirties the element, and this runs on a timer.
            if (text == _lastText)
                return;

            _lastText = text;
            _state.text = text;
            _state.style.opacity = armed ? 1f : 0.6f;
        }
    }
}
