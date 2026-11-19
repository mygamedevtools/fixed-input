using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGameDevTools.FixedInput.Samples
{
    /// <summary>A corner overlay showing what crosses an input surface, step by step.</summary>
    [AddComponentMenu("Fixed Input/Samples/Input Surface Panel")]
    [RequireComponent(typeof(UIDocument))]
    public class InputSurfacePanel : MonoBehaviour
    {
        const string Block = "fixed-input-panel";

        // Indexed by IntentActivity, so a cell's class follows its activity without a switch.
        static readonly string[] CellStates =
        {
            null,
            Block + "__cell--set",
            Block + "__cell--waiting",
            Block + "__cell--consumed",
            Block + "__cell--set-and-consumed",
            Block + "__cell--expired",
        };

        const float SmoothingSeconds = 0.25f;

        [SerializeField]
        [Tooltip("Overlay width in reference pixels. The UIDocument's Panel Settings scale it from there.")]
        int _width = 430;

        [SerializeField]
        [Tooltip("How many fixed steps each lane keeps.")]
        int _historySteps = 48;

        [SerializeField]
        [Tooltip("Show the frame rate readout and the rate and time scale controls.")]
        bool _showFrameControls = true;

        [SerializeField]
        [Tooltip("The overlay's own look, InputSurfacePanel.uss.")]
        StyleSheet _styleSheet;

        [SerializeField]
        [Tooltip("Style sheets added after the overlay's own, to restyle it. Declare --fixed-input-* variables on :root in them; see InputSurfacePanel.uss for the list.")]
        StyleSheet[] _styleSheets = Array.Empty<StyleSheet>();

        readonly List<WatchedSurface> _surfaces = new List<WatchedSurface>();
        readonly List<Chip> _chips = new List<Chip>();

        UIDocument _document;
        Label _tickLabel, _fpsLabel, _hzLabel, _ratioLabel, _ratioUnit;
        VisualElement _chipHost;
        bool _built;

        float _fpsSmoothed = 60f;
        int _stepsThisFrame, _stepsSinceRender;
        float _stepsPerFrameSmoothed = 1f;

        int _originalVSync;
        int _originalTargetFrameRate;
        float _originalTimeScale = 1f;
        bool _touchedGlobals;

        sealed class Chip
        {
            public string Label;
            public Func<bool> IsDown;
            public VisualElement Element;
        }

        sealed class WatchedSurface
        {
            public object Target;
            public FieldInfo[] Fields;
            public IntentTrack[] Tracks;
            public VisualElement[] Strips;
            public int[][] CellStates;
            public Label[] Ages;
        }

        void Awake()
        {
            _document = GetComponent<UIDocument>();

            // An overlay you cannot see while the editor is unfocused is not much of an overlay.
            Application.runInBackground = true;
        }

        void OnEnable()
        {
            MyFixedTick.StepClosing += OnStepClosing;

            _originalVSync = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;
            _originalTimeScale = Time.timeScale;
        }

        void OnDisable()
        {
            MyFixedTick.StepClosing -= OnStepClosing;

            // Frame rate and time scale are global.
            if (!_touchedGlobals)
                return;

            QualitySettings.vSyncCount = _originalVSync;
            Application.targetFrameRate = _originalTargetFrameRate;
            Time.timeScale = _originalTimeScale;
            _touchedGlobals = false;
        }

        /// <summary>Follows every intent on a surface.</summary>
        public void Watch(object surface, string name = null)
        {
            if (surface == null)
                return;

            foreach (WatchedSurface known in _surfaces)
            {
                if (ReferenceEquals(known.Target, surface))
                    return;
            }

            List<FieldInfo> fields = new List<FieldInfo>();

            for (Type type = surface.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(FixedInputEvent))
                        fields.Add(field);
                }
            }

            if (fields.Count == 0)
                return;

            int capacity = Mathf.Max(8, _historySteps);

            WatchedSurface watched = new WatchedSurface
            {
                Target = surface,
                Fields = fields.ToArray(),
                Tracks = new IntentTrack[fields.Count],
                Strips = new VisualElement[fields.Count],
                CellStates = new int[fields.Count][],
                Ages = new Label[fields.Count]
            };

            for (int i = 0; i < fields.Count; i++)
            {
                uint window = fields[i].GetCustomAttribute<FixedInputWindowAttribute>()?.Window ?? 0;
                watched.Tracks[i] = new IntentTrack(fields[i].Name, window, capacity);
            }

            _surfaces.Add(watched);
            _built = false;
        }

        /// <summary>Adds a key cap to the header, lit while is true.</summary>
        public void AddChip(string label, Func<bool> isDown)
        {
            if (string.IsNullOrEmpty(label) || isDown == null)
                return;

            _chips.Add(new Chip { Label = label, IsDown = isDown });
            _built = false;
        }

        /// <summary>Whether the overlay samples on its own when a step closes.</summary>
        public bool SampleOnStepClosing { get; set; } = true;

        /// <summary>Records every watched surface against the closing tick.</summary>
        public void SampleNow(uint closingTick)
        {
            _stepsSinceRender++;

            foreach (WatchedSurface surface in _surfaces)
            {
                for (int i = 0; i < surface.Fields.Length; i++)
                {
                    FixedInputEvent value = (FixedInputEvent)surface.Fields[i].GetValue(surface.Target);
                    surface.Tracks[i].Sample(value, closingTick);
                }
            }
        }

        void OnStepClosing(uint closingTick)
        {
            if (SampleOnStepClosing)
                SampleNow(closingTick);
            else
                _stepsSinceRender++;
        }

        void Update()
        {
            MeasureFrame();

            if (!_built)
                Build();

            if (_built)
                Render();
        }

        void MeasureFrame()
        {
            float ms = Time.unscaledDeltaTime * 1000f;

            // Smoothed over a fixed quarter second of wall time rather than a fixed number of
            // frames.
            float alpha = 1f - Mathf.Exp(-Time.unscaledDeltaTime / SmoothingSeconds);

            float fps = ms > 0.0001f ? 1000f / ms : 0f;
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, fps, alpha);

            _stepsThisFrame = _stepsSinceRender;
            _stepsSinceRender = 0;
            _stepsPerFrameSmoothed = Mathf.Lerp(_stepsPerFrameSmoothed, _stepsThisFrame, alpha);
        }

        // ---- construction ----------------------------------------------------

        void Build()
        {
            VisualElement root = _document == null ? null : _document.rootVisualElement;

            if (root == null)
                return;

            root.Clear();

            Attach(root, _styleSheet);

            // After the overlay's own sheet, so their variables are the ones it reads.
            foreach (StyleSheet extra in _styleSheets)
                Attach(root, extra);

            VisualElement panel = Element(Block);
            panel.style.width = _width;
            root.Add(panel);

            panel.Add(BuildAccent());
            panel.Add(BuildHeader());

            foreach (WatchedSurface surface in _surfaces)
                BuildLanes(panel, surface);

            if (_showFrameControls)
            {
                panel.Add(Element(Block + "__rule"));
                panel.Add(BuildFrameRow());
                panel.Add(BuildControls());
            }

            _built = true;
        }

        VisualElement BuildHeader()
        {
            VisualElement header = Element(Block + "__header");
            VisualElement tickGroup = Element(Block + "__tick-group");

            tickGroup.Add(Text("TICK", Block + "__caption"));
            _tickLabel = Text("0", Block + "__tick");
            tickGroup.Add(_tickLabel);
            header.Add(tickGroup);

            _chipHost = Element(Block + "__chips");

            foreach (Chip chip in _chips)
            {
                chip.Element = Element(Block + "__chip");
                chip.Element.Add(Text(chip.Label, Block + "__chip-label"));
                _chipHost.Add(chip.Element);
            }

            header.Add(_chipHost);
            return header;
        }

        static VisualElement BuildAccent()
        {
            VisualElement accent = Element(Block + "__accent");
            accent.Add(Element(Block + "__accent-segment"));
            accent.Add(Element(Block + "__accent-segment", Block + "__accent-segment--2"));
            accent.Add(Element(Block + "__accent-segment", Block + "__accent-segment--3"));
            return accent;
        }

        void BuildLanes(VisualElement panel, WatchedSurface surface)
        {
            for (int i = 0; i < surface.Fields.Length; i++)
            {
                VisualElement lane = Element(Block + "__lane");
                lane.Add(Text(surface.Fields[i].Name, Block + "__lane-name"));

                VisualElement strip = Element(Block + "__strip");

                for (int cell = 0; cell < surface.Tracks[i].Capacity; cell++)
                    strip.Add(Element(Block + "__cell"));

                lane.Add(strip);
                surface.Strips[i] = strip;
                surface.CellStates[i] = new int[strip.childCount];

                Label age = Text("—", Block + "__age");
                lane.Add(age);
                surface.Ages[i] = age;

                panel.Add(lane);
            }
        }

        VisualElement BuildFrameRow()
        {
            VisualElement row = Element(Block + "__frame-row");

            _fpsLabel = Text("60", Block + "__value");
            row.Add(_fpsLabel);
            row.Add(Text("fps", Block + "__unit"));

            row.Add(Text("·", Block + "__separator"));

            _hzLabel = Text("50", Block + "__value");
            row.Add(_hzLabel);
            row.Add(Text("Hz", Block + "__unit"));

            row.Add(Text("·", Block + "__separator"));

            _ratioLabel = Text("1.0", Block + "__value");
            row.Add(_ratioLabel);
            _ratioUnit = Text("frames/step", Block + "__unit");
            row.Add(_ratioUnit);

            return row;
        }

        VisualElement BuildControls()
        {
            VisualElement row = Element(Block + "__controls");

            row.Add(Text("FPS", Block + "__control-label"));

            List<Button> rateButtons = new List<Button>();
            AddPreset(row, rateButtons, "max", () => SetFrameRate(0), true);
            AddPreset(row, rateButtons, "144", () => SetFrameRate(144), false);
            AddPreset(row, rateButtons, "60", () => SetFrameRate(60), false);
            AddPreset(row, rateButtons, "30", () => SetFrameRate(30), false);
            AddPreset(row, rateButtons, "12", () => SetFrameRate(12), false);

            row.Add(Text("SCALE", Block + "__control-label", Block + "__control-label--spaced"));

            List<Button> scaleButtons = new List<Button>();
            AddPreset(row, scaleButtons, "1", () => SetTimeScale(1f), true);
            AddPreset(row, scaleButtons, ".25", () => SetTimeScale(0.25f), false);
            AddPreset(row, scaleButtons, ".05", () => SetTimeScale(0.05f), false);

            return row;
        }

        void AddPreset(VisualElement row, List<Button> group, string label, Action action, bool active)
        {
            Button button = null;

            button = new Button(() =>
            {
                action();

                foreach (Button other in group)
                    Highlight(other, other == button);
            })
            { text = label };

            button.AddToClassList(Block + "__button");
            Highlight(button, active);
            group.Add(button);
            row.Add(button);
        }

        // ---- controls --------------------------------------------------------

        /// <summary>Caps the render rate, or uncaps it at zero.</summary>
        public void SetFrameRate(int fps)
        {
            _touchedGlobals = true;

            if (fps <= 0)
            {
                QualitySettings.vSyncCount = _originalVSync;
                Application.targetFrameRate = _originalTargetFrameRate;
                return;
            }

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fps;
        }

        /// <summary>Runs at times real time.</summary>
        public void SetTimeScale(float scale)
        {
            _touchedGlobals = true;
            Time.timeScale = Mathf.Max(0f, scale);
        }

        // ---- per-frame -------------------------------------------------------

        void Render()
        {
            uint tick = MyFixedTick.Current;
            _tickLabel.text = tick.ToString();

            foreach (Chip chip in _chips)
            {
                if (chip.Element == null)
                    continue;

                bool down = false;

                try
                {
                    down = chip.IsDown();
                }
                catch
                {
                    // A throwing probe must not take the overlay down with it.
                }

                chip.Element.EnableInClassList(Block + "__chip--down", down);
            }

            foreach (WatchedSurface surface in _surfaces)
            {
                for (int i = 0; i < surface.Fields.Length; i++)
                {
                    FixedInputEvent value = (FixedInputEvent)surface.Fields[i].GetValue(surface.Target);
                    IntentTrack track = surface.Tracks[i];

                    Label age = surface.Ages[i];
                    age.text = Age(value, tick, track, out AgeState state);
                    age.EnableInClassList(Block + "__age--idle", state == AgeState.Idle);
                    age.EnableInClassList(Block + "__age--armed", state == AgeState.Armed);
                    age.EnableInClassList(Block + "__age--lost", state == AgeState.Lost);

                    PaintStrip(surface.Strips[i], surface.CellStates[i], track);
                }
            }

            if (_showFrameControls)
                RenderFrameStrip();
        }

        void RenderFrameStrip()
        {
            _fpsLabel.text = Mathf.RoundToInt(_fpsSmoothed).ToString();

            _hzLabel.text = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.fixedDeltaTime)).ToString();

            // Below one step per frame the useful number inverts to frames between steps, which is how
            // many presses an Update-phase writer can collapse into one.
            float steps = _stepsPerFrameSmoothed;

            bool busy = steps >= 1f;

            if (busy)
            {
                _ratioLabel.text = steps.ToString("0.0");
                _ratioUnit.text = "steps/frame";
            }
            else
            {
                _ratioLabel.text = steps > 0.001f ? (1f / steps).ToString("0.0") : "—";
                _ratioUnit.text = "frames/step";
            }

            _ratioLabel.EnableInClassList(Block + "__value--busy", busy);
            _ratioUnit.EnableInClassList(Block + "__unit--busy", busy);
        }

        enum AgeState { Idle, Armed, Lost }

        static string Age(in FixedInputEvent value, uint tick, IntentTrack track, out AgeState state)
        {
            if (!value.IsArmed)
            {
                state = AgeState.Idle;
                return track.LastConsume.age < 0 ? "—" : "+" + track.LastConsume.age;
            }

            uint age = unchecked(tick - value.SetTick);

            if (age > uint.MaxValue / 2)
            {
                state = AgeState.Armed;
                return "next";
            }

            if (age > track.Window)
            {
                state = AgeState.Lost;
                return "lost";
            }

            state = AgeState.Armed;
            return "+" + age;
        }

        static void PaintStrip(VisualElement strip, int[] states, IntentTrack track)
        {
            int cells = strip.childCount;

            for (int i = 0; i < cells; i++)
            {
                // Newest on the right, so the strip reads like a chart scrolling left.
                int activity = (int)track.ActivityAt(cells - 1 - i);

                if (activity < 0 || activity >= CellStates.Length)
                    activity = 0;

                if (states[i] == activity)
                    continue;

                VisualElement cell = strip[i];

                if (CellStates[states[i]] != null)
                    cell.RemoveFromClassList(CellStates[states[i]]);

                if (CellStates[activity] != null)
                    cell.AddToClassList(CellStates[activity]);

                states[i] = activity;
            }
        }

        // ---- small builders --------------------------------------------------

        static void Attach(VisualElement root, StyleSheet sheet)
        {
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);
        }

        static VisualElement Element(params string[] classes)
        {
            VisualElement element = new VisualElement();

            foreach (string name in classes)
                element.AddToClassList(name);

            return element;
        }

        static Label Text(string text, params string[] classes)
        {
            Label label = new Label(text);

            foreach (string name in classes)
                label.AddToClassList(name);

            return label;
        }

        static void Highlight(Button button, bool active) =>
            button.EnableInClassList(Block + "__button--active", active);
    }
}
