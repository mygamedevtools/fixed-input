using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace MyGameDevTools.FixedInput.Editor
{
    /// <summary>Draws a as readable state instead of a raw integer nobody can interpret.</summary>
    [CustomPropertyDrawer(typeof(FixedInputEvent))]
    public class FixedInputEventDrawer : PropertyDrawer
    {
        /// <summary>Ten times a second.</summary>
        const long RefreshIntervalMs = 100;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty armed = property.FindPropertyRelative("_armed");
            SerializedProperty setTick = property.FindPropertyRelative("_setTick");

            uint window = fieldInfo?.GetCustomAttribute<FixedInputWindowAttribute>()?.Window ?? 0;

            FixedInputEventView view = new FixedInputEventView(property.displayName);

            // Cached as primitives, because a disposed SerializedProperty throws and a cached bool cannot.
            bool cachedArmed = armed.boolValue;
            uint cachedSetTick = setTick.uintValue;

            void Refresh()
            {
                bool live = EditorApplication.isPlaying;
                view.Refresh(cachedArmed, cachedSetTick, live ? MyFixedTick.Current : 0u, live, window);
            }

            // TrackPropertyValue catches the transitions.
            view.TrackPropertyValue(armed, changed =>
            {
                cachedArmed = changed.boolValue;
                Refresh();
            });

            view.TrackPropertyValue(setTick, changed =>
            {
                cachedSetTick = changed.uintValue;
                Refresh();
            });

            IVisualElementScheduledItem ticker = view.schedule.Execute(Refresh).Every(RefreshIntervalMs);
            ticker.Pause();

            // Paused outside play mode, or every inspector showing an intent would keep the editor
            // awake at idle.
            view.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (EditorApplication.isPlaying)
                    ticker.Resume();
            });

            view.RegisterCallback<DetachFromPanelEvent>(_ => ticker.Pause());

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            view.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeStateChanged);

            void OnPlayModeStateChanged(PlayModeStateChange change)
            {
                if (change == PlayModeStateChange.EnteredPlayMode)
                    ticker.Resume();
                else if (change == PlayModeStateChange.ExitingPlayMode)
                    ticker.Pause();

                Refresh();
            }

            Refresh();
            return view;
        }
    }
}
