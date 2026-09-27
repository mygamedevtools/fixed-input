using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;
using UnityEngine;

namespace MyGameDevTools.FixedInput
{
    /// <summary>Turns the two silent failures of an input surface into messages that name the field.</summary>
    public static class FixedInputDiagnostics
    {
        /// <summary>Whether the checks run.</summary>
        public static bool Enabled { get; set; } =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        /// <summary>The window assumed for a field with no FixedInputWindow attribute.</summary>
        public static uint DefaultWindow { get; set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly List<WeakReference<object>> _surfaces = new List<WeakReference<object>>();
        static readonly Dictionary<Type, FieldInfo[]> _fieldsByType = new Dictionary<Type, FieldInfo[]>();
        static readonly Dictionary<FieldInfo, uint> _windowByField = new Dictionary<FieldInfo, uint>();
        static readonly Dictionary<WriterKey, WriteRecord> _writes = new Dictionary<WriterKey, WriteRecord>();
        static readonly HashSet<WriterKey> _reportedDoubleWrite = new HashSet<WriterKey>();
        static readonly HashSet<WriterKey> _reportedUnconsumed = new HashSet<WriterKey>();
#endif

        /// <summary>Watches an input surface for intents nobody consumes.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Register(object surface)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (surface == null)
                return;

            foreach (WeakReference<object> known in _surfaces)
            {
                if (known.TryGetTarget(out object target) && ReferenceEquals(target, surface))
                    return;
            }

            _surfaces.Add(new WeakReference<object>(surface));
#endif
        }

        /// <summary>Stops watching an input surface.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Unregister(object surface)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = _surfaces.Count - 1; i >= 0; i--)
            {
                if (!_surfaces[i].TryGetTarget(out object target) || ReferenceEquals(target, surface))
                    _surfaces.RemoveAt(i);
            }
#endif
        }

        /// <summary>Records that a writer set an intent, so a second writer in the same step can be named.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void NoteSet(
            object surface,
            string field,
            uint tick,
            [CallerFilePath] string callerFile = "",
            [CallerLineNumber] int callerLine = 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Enabled || surface == null)
                return;

            WriterKey key = new WriterKey(surface, field);
            string site = $"{callerFile}:{callerLine}";

            if (_writes.TryGetValue(key, out WriteRecord previous) && previous.Tick == tick && _reportedDoubleWrite.Add(key))
            {

                if (previous.Site != site)
                {
                    Debug.LogError(
                        $"[FixedInput] '{field}' on {Describe(surface)} was set twice during tick {tick}, by two different writers:\n" +
                        $"  {previous.Site}\n  {site}\n" +
                        "Exactly one writer may fill an input surface per frame. The usual cause is a scripted writer " +
                        "driving a character whose device writer is still enabled, so one of the two presses is lost.",
                        surface as UnityEngine.Object);
                }
                else
                {
                    Debug.LogWarning(
                        $"[FixedInput] '{field}' on {Describe(surface)} was set twice during tick {tick} from the same place ({site}), " +
                        "so the first press was overwritten before anything could consume it. A writer running in Update " +
                        "does this whenever the frame rate outruns the fixed rate; move it to FixedUpdate if every press must count.",
                        surface as UnityEngine.Object);
                }
            }

            _writes[key] = new WriteRecord(tick, site);
#endif
        }

        /// <summary>Reports every registered intent that is armed and past its window.</summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void CheckForUnconsumed(uint tick)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Enabled)
                return;

            for (int i = _surfaces.Count - 1; i >= 0; i--)
            {
                if (!_surfaces[i].TryGetTarget(out object surface) || surface is UnityEngine.Object dead && dead == null)
                {
                    _surfaces.RemoveAt(i);
                    continue;
                }

                foreach (FieldInfo field in FieldsOf(surface.GetType()))
                {
                    FixedInputEvent value = (FixedInputEvent)field.GetValue(surface);

                    WriterKey key = new WriterKey(surface, field.Name);

                    if (!value.IsArmed)
                    {
                        // Consumed or cleared, so the incident is over and the next one is worth
                        // hearing about.
                        _reportedUnconsumed.Remove(key);
                        continue;
                    }

                    uint window = WindowOf(field);
                    uint age = unchecked(tick - value.SetTick);

                    // A future set tick underflows to an enormous age, which is an Update-phase
                    // writer waiting for its step rather than anything lost.
                    if (age <= window || age > uint.MaxValue / 2)
                        continue;

                    if (!_reportedUnconsumed.Add(key))
                        continue;

                    Debug.LogWarning(
                        $"[FixedInput] '{field.Name}' on {Describe(surface)} was set at tick {value.SetTick} and never consumed " +
                        $"(now tick {tick}, window {window}).\n" +
                        "Either nothing reads this field, or its reader runs before its writer in the same step. " +
                        "A writer in FixedUpdate must execute before its readers.",
                        surface as UnityEngine.Object);
                }
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnEnteringPlayMode() => ResetState();

        /// <summary>Forgets every watched surface, recorded write and suppressed report.</summary>
        internal static void ResetState()
        {
            _surfaces.Clear();
            _writes.Clear();
            _reportedDoubleWrite.Clear();
            _reportedUnconsumed.Clear();
            DefaultWindow = 0;
            Enabled = true;
        }

        static FieldInfo[] FieldsOf(Type type)
        {
            if (_fieldsByType.TryGetValue(type, out FieldInfo[] cached))
                return cached;

            List<FieldInfo> found = new List<FieldInfo>();

            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType == typeof(FixedInputEvent))
                        found.Add(field);
                }
            }

            cached = found.ToArray();
            _fieldsByType[type] = cached;
            return cached;
        }

        static uint WindowOf(FieldInfo field)
        {
            if (_windowByField.TryGetValue(field, out uint cached))
                return cached;

            cached = field.GetCustomAttribute<FixedInputWindowAttribute>()?.Window ?? DefaultWindow;
            _windowByField[field] = cached;
            return cached;
        }

        static string Describe(object surface)
        {
            if (surface is Component component && component != null)
                return $"{component.GetType().Name} on '{component.gameObject.name}'";

            return surface.GetType().Name;
        }

        readonly struct WriterKey : IEquatable<WriterKey>
        {
            readonly object _surface;
            readonly string _field;

            public WriterKey(object surface, string field)
            {
                _surface = surface;
                _field = field;
            }

            public bool Equals(WriterKey other) => ReferenceEquals(_surface, other._surface) && _field == other._field;

            public override bool Equals(object obj) => obj is WriterKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_surface), _field);
        }

        readonly struct WriteRecord
        {
            public readonly uint Tick;
            public readonly string Site;

            public WriteRecord(uint tick, string site)
            {
                Tick = tick;
                Site = site;
            }
        }
#endif
    }
}
