using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGameDevTools.FixedInput
{
    /// <summary>A corner overlay showing what crosses an input surface, step by step.</summary>
    [AddComponentMenu("Fixed Input/Input Surface Panel")]
    [RequireComponent(typeof(UIDocument))]
    public class InputSurfacePanel : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.933f, 0.945f, 0.965f);
        static readonly Color Muted = new Color(0.725f, 0.761f, 0.824f);
        static readonly Color Dim = new Color(0.545f, 0.584f, 0.655f);
        static readonly Color Line = new Color(0.588f, 0.667f, 0.804f, 0.30f);
        static readonly Color Glass = new Color(0.035f, 0.047f, 0.071f, 0.95f);
        static readonly Color Fill = new Color(0.588f, 0.686f, 0.843f, 0.17f);
        static readonly Color LaneEmpty = new Color(0.588f, 0.686f, 0.843f, 0.08f);

        static readonly Color Set = new Color(0.957f, 0.663f, 0.227f);
        static readonly Color Waiting = new Color(0.957f, 0.663f, 0.227f, 0.32f);
        static readonly Color Consumed = new Color(0.208f, 0.761f, 0.647f);
        static readonly Color Expired = new Color(0.910f, 0.365f, 0.416f);
        static readonly Color Device = new Color(0.561f, 0.635f, 0.949f);

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

            VisualElement ribbon = new VisualElement();
            ribbon.style.position = Position.Absolute;
            ribbon.style.left = 20;
            ribbon.style.top = 20;
            ribbon.style.width = _width;
            ribbon.style.maxWidth = Length.Percent(92);
            ribbon.style.backgroundColor = Glass;
            ribbon.style.borderTopLeftRadius = ribbon.style.borderTopRightRadius = 5;
            ribbon.style.borderBottomLeftRadius = ribbon.style.borderBottomRightRadius = 5;
            SetBorder(ribbon, 1, Line);
            Pad(ribbon, 14, 11);
            root.Add(ribbon);

            ribbon.Add(BuildHeader());

            foreach (WatchedSurface surface in _surfaces)
                BuildLanes(ribbon, surface);

            if (_showFrameControls)
            {
                ribbon.Add(Rule());
                ribbon.Add(BuildFrameRow());
                ribbon.Add(BuildControls());
            }

            _built = true;
        }

        VisualElement BuildHeader()
        {
            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.marginBottom = 8;

            VisualElement left = new VisualElement();
            left.style.flexDirection = FlexDirection.Row;
            left.style.alignItems = Align.FlexEnd;

            Label caption = Text("TICK", 12, Muted);
            caption.style.marginRight = 7;
            caption.style.marginBottom = 3;
            caption.style.letterSpacing = 1.5f;
            left.Add(caption);

            _tickLabel = Text("0", 26, Ink);
            _tickLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            left.Add(_tickLabel);

            header.Add(left);

            _chipHost = new VisualElement();
            _chipHost.style.flexDirection = FlexDirection.Row;

            foreach (Chip chip in _chips)
            {
                chip.Element = KeyCap(chip.Label);
                _chipHost.Add(chip.Element);
            }

            header.Add(_chipHost);
            return header;
        }

        void BuildLanes(VisualElement ribbon, WatchedSurface surface)
        {
            for (int i = 0; i < surface.Fields.Length; i++)
            {
                VisualElement lane = new VisualElement();
                lane.style.flexDirection = FlexDirection.Row;
                lane.style.alignItems = Align.Center;
                lane.style.marginBottom = 4;

                Label name = Text(surface.Fields[i].Name, 14, Ink);
                name.style.width = 74;
                name.style.flexShrink = 0;
                lane.Add(name);

                VisualElement strip = new VisualElement();
                strip.style.flexDirection = FlexDirection.Row;
                strip.style.flexGrow = 1;
                strip.style.height = 15;

                for (int cell = 0; cell < surface.Tracks[i].Capacity; cell++)
                {
                    VisualElement mark = new VisualElement();
                    mark.style.flexGrow = 1;
                    mark.style.marginRight = 1;
                    mark.style.backgroundColor = LaneEmpty;
                    strip.Add(mark);
                }

                lane.Add(strip);
                surface.Strips[i] = strip;

                Label age = Text("—", 13, Muted);
                age.style.width = 40;
                age.style.flexShrink = 0;
                age.style.marginLeft = 8;
                age.style.unityTextAlign = TextAnchor.MiddleRight;
                lane.Add(age);
                surface.Ages[i] = age;

                ribbon.Add(lane);
            }
        }

        VisualElement BuildFrameRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            _fpsLabel = Text("60", 14, Ink);
            row.Add(_fpsLabel);
            row.Add(Unit("fps"));

            row.Add(Separator());

            _hzLabel = Text("50", 14, Ink);
            row.Add(_hzLabel);
            row.Add(Unit("Hz"));

            row.Add(Separator());

            _ratioLabel = Text("1.0", 14, Ink);
            row.Add(_ratioLabel);
            _ratioUnit = Unit("frames/step");
            row.Add(_ratioUnit);

            return row;
        }

        VisualElement BuildControls()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginTop = 8;

            row.Add(ControlLabel("fps"));

            List<Button> rateButtons = new List<Button>();
            AddPreset(row, rateButtons, "max", () => SetFrameRate(0), true);
            AddPreset(row, rateButtons, "144", () => SetFrameRate(144), false);
            AddPreset(row, rateButtons, "60", () => SetFrameRate(60), false);
            AddPreset(row, rateButtons, "30", () => SetFrameRate(30), false);
            AddPreset(row, rateButtons, "12", () => SetFrameRate(12), false);

            Label scale = ControlLabel("scale");
            scale.style.marginLeft = 10;
            row.Add(scale);

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

            StyleButton(button);
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

                chip.Element.style.backgroundColor = down ? Device : Fill;
                ((Label)chip.Element[0]).style.color = down ? new Color(0.043f, 0.051f, 0.071f) : Ink;
            }

            foreach (WatchedSurface surface in _surfaces)
            {
                for (int i = 0; i < surface.Fields.Length; i++)
                {
                    FixedInputEvent value = (FixedInputEvent)surface.Fields[i].GetValue(surface.Target);
                    IntentTrack track = surface.Tracks[i];

                    surface.Ages[i].text = Age(value, tick, track, out Color colour);
                    surface.Ages[i].style.color = colour;

                    PaintStrip(surface.Strips[i], track);
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

            if (steps >= 1f)
            {
                _ratioLabel.text = steps.ToString("0.0");
                _ratioUnit.text = "steps/frame";
                _ratioLabel.style.color = Set;
                _ratioUnit.style.color = Set;
            }
            else
            {
                _ratioLabel.text = steps > 0.001f ? (1f / steps).ToString("0.0") : "—";
                _ratioUnit.text = "frames/step";
                _ratioLabel.style.color = Ink;
                _ratioUnit.style.color = Muted;
            }
        }

        static string Age(in FixedInputEvent value, uint tick, IntentTrack track, out Color colour)
        {
            if (!value.IsArmed)
            {
                colour = Dim;
                return track.LastConsume.age < 0 ? "—" : "+" + track.LastConsume.age;
            }

            uint age = unchecked(tick - value.SetTick);

            if (age > uint.MaxValue / 2)
            {
                colour = Set;
                return "next";
            }

            if (age > track.Window)
            {
                colour = Expired;
                return "lost";
            }

            colour = Set;
            return "+" + age;
        }

        static void PaintStrip(VisualElement strip, IntentTrack track)
        {
            int cells = strip.childCount;

            for (int i = 0; i < cells; i++)
            {
                // Newest on the right, so the strip reads like a chart scrolling left.
                IntentActivity activity = track.ActivityAt(cells - 1 - i);
                VisualElement cell = strip[i];

                cell.style.backgroundColor = activity switch
                {
                    IntentActivity.Set => Set,
                    IntentActivity.SetAndConsumed => Consumed,
                    IntentActivity.Waiting => Waiting,
                    IntentActivity.Consumed => Consumed,
                    IntentActivity.Expired => Expired,
                    _ => LaneEmpty
                };

                // Set and consumed inside one step is the normal result of a writer in FixedUpdate,
                // and it would otherwise look identical to a consume of something set earlier.
                bool bothInOneStep = activity == IntentActivity.SetAndConsumed;
                cell.style.borderTopWidth = bothInOneStep ? 4 : 0;
                cell.style.borderTopColor = Set;
            }
        }

        // ---- small builders --------------------------------------------------

        static VisualElement Rule()
        {
            VisualElement rule = new VisualElement();
            rule.style.height = 1;
            rule.style.backgroundColor = Line;
            rule.style.marginTop = 7;
            rule.style.marginBottom = 8;
            return rule;
        }

        static VisualElement KeyCap(string label)
        {
            VisualElement cap = new VisualElement();
            cap.style.minWidth = 26;
            cap.style.height = 22;
            cap.style.marginLeft = 4;
            cap.style.backgroundColor = Fill;
            cap.style.alignItems = Align.Center;
            cap.style.justifyContent = Justify.Center;
            cap.style.paddingLeft = cap.style.paddingRight = 6;
            cap.style.borderTopLeftRadius = cap.style.borderTopRightRadius = 3;
            cap.style.borderBottomLeftRadius = cap.style.borderBottomRightRadius = 3;
            SetBorder(cap, 1, Line);

            Label text = Text(label, 13, Ink);
            text.style.unityFontStyleAndWeight = FontStyle.Bold;
            cap.Add(text);

            return cap;
        }

        static Label Unit(string text)
        {
            Label label = Text(text, 12, Muted);
            label.style.marginLeft = 3;
            return label;
        }

        static Label Separator()
        {
            Label label = Text("·", 12, Dim);
            label.style.marginLeft = 7;
            label.style.marginRight = 7;
            return label;
        }

        static Label ControlLabel(string text)
        {
            Label label = Text(text.ToUpperInvariant(), 11, Muted);
            label.style.letterSpacing = 1.3f;
            label.style.marginRight = 5;
            return label;
        }

        static Label Text(string text, int size, Color colour)
        {
            Label label = new Label(text);
            label.style.fontSize = size;
            label.style.color = colour;
            return label;
        }

        static void StyleButton(Button button)
        {
            button.style.fontSize = 13;
            button.style.marginLeft = 0;
            button.style.marginRight = 3;
            button.style.marginTop = button.style.marginBottom = 0;
            button.style.paddingLeft = button.style.paddingRight = 7;
            button.style.paddingTop = button.style.paddingBottom = 3;
            button.style.minWidth = 30;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 3;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 3;
            SetBorder(button, 1, Line);
        }

        static void Highlight(Button button, bool active)
        {
            button.style.backgroundColor = active ? Set : Fill;
            button.style.color = active ? new Color(0.043f, 0.051f, 0.071f) : Ink;
        }

        static void Pad(VisualElement element, int horizontal, int vertical)
        {
            element.style.paddingLeft = element.style.paddingRight = horizontal;
            element.style.paddingTop = element.style.paddingBottom = vertical;
        }

        static void SetBorder(VisualElement element, int width, Color colour)
        {
            element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = element.style.borderRightWidth = width;
            element.style.borderTopColor = colour;
            element.style.borderBottomColor = colour;
            element.style.borderLeftColor = colour;
            element.style.borderRightColor = colour;
        }
    }
}
