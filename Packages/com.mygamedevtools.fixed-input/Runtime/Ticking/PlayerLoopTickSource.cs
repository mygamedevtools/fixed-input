using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using FixedUpdatePhase = UnityEngine.PlayerLoop.FixedUpdate;

namespace MyGameDevTools.FixedInput
{
    /// <summary>The default tick source.</summary>
    public sealed partial class PlayerLoopTickSource : ITickSource
    {
        /// <summary>Identifies our entry in the player loop.</summary>
        public struct FixedInputTickUpdate { }

        static PlayerLoopTickSource _instance;

        /// <summary>The shared instance.</summary>
        public static PlayerLoopTickSource Instance => _instance ??= new PlayerLoopTickSource();

        /// <summary>Whether our system is currently present in the player loop.</summary>
        public static bool IsInstalled => FindMarker(PlayerLoop.GetCurrentPlayerLoop());

        public uint Tick { get; private set; }

        // Engine fixed time as of our last increment.
        internal float LastAdvanceFixedTime { get; private set; }

        PlayerLoopTickSource() => LastAdvanceFixedTime = Time.fixedTime;

        void Advance()
        {
            // Before the increment, so both see the step that has just finished running, with every
            // reader in it already given its chance.
            uint authoritative = MyFixedTick.Current;
            FixedInputDiagnostics.CheckForUnconsumed(authoritative);
            MyFixedTick.RaiseStepClosing(authoritative);

            Tick++;
            LastAdvanceFixedTime = Time.fixedTime;
        }

        /// <summary>
        /// Whether fixed steps have run without our increment, which means another package replaced
        /// the player loop and dropped us.
        /// </summary>
        internal bool DetectEviction()
        {
            if (!Application.isPlaying)
                return false;

            // A reader inside FixedUpdate legitimately sees a full step of gap, because the
            // increment runs at the end of the phase and has not happened yet for the step the
            // reader is in.
            if (Time.fixedTime - LastAdvanceFixedTime <= Time.fixedDeltaTime * 1.5f)
                return false;

            // The gap alone is only a suspicion, since an editor stall reads the same; walking the loop
            // answers definitively.
            return !IsInstalled;
        }

        /// <summary>
        /// Puts the increment back at the end of the fixed-update phase, after another package has
        /// removed or displaced it.
        /// </summary>
        public static void Reinstall() => Install();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnEnteringPlayMode()
        {
            // Statics survive when Enter Play Mode has domain reload disabled, so a previous
            // session's count would otherwise carry over and the install guard would skip the
            // install of a loop that was rebuilt, so ticks work once and never again.
            Instance.Tick = 0;
            Instance.LastAdvanceFixedTime = Time.fixedTime;
            Install();
        }

#if UNITY_6000_5_OR_NEWER
        [OnExitingPlayMode]
        static void OnExitingPlayMode() => Uninstall();
#elif UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void SubscribeToPlayModeExit()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                Uninstall();
        }
#endif

        /// <summary>
        /// Appends the increment to the end of the fixed-update phase, replacing any copy already
        /// there.
        /// </summary>
        internal static void Install()
        {
            // Always the CURRENT loop, never the default one.
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            RemoveMarker(ref loop);

            PlayerLoopSystem increment = new PlayerLoopSystem
            {
                type = typeof(FixedInputTickUpdate),
                updateDelegate = Instance.Advance
            };

            if (!AppendToFixedUpdate(ref loop, increment))
            {
                Debug.LogError($"[{nameof(PlayerLoopTickSource)}] No FixedUpdate phase found in the player loop, so the tick cannot advance. Set {nameof(MyFixedTick)}.{nameof(MyFixedTick.Source)} to a source this project controls.");
                return;
            }

            PlayerLoop.SetPlayerLoop(loop);

            // Start measuring from now.
            Instance.LastAdvanceFixedTime = Time.fixedTime;
        }

        /// <summary>Removes our increment and leaves every other system untouched.</summary>
        internal static void Uninstall()
        {
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();

            // Only write the loop back if we actually found something, so an uninstall on a loop we
            // are not in cannot clobber a change another package made in between.
            if (RemoveMarker(ref loop))
                PlayerLoop.SetPlayerLoop(loop);
        }

        static bool AppendToFixedUpdate(ref PlayerLoopSystem node, PlayerLoopSystem system)
        {
            if (node.subSystemList == null)
                return false;

            for (int i = 0; i < node.subSystemList.Length; i++)
            {
                if (node.subSystemList[i].type == typeof(FixedUpdatePhase))
                {
                    List<PlayerLoopSystem> children = node.subSystemList[i].subSystemList == null
                        ? new List<PlayerLoopSystem>(1)
                        : new List<PlayerLoopSystem>(node.subSystemList[i].subSystemList);

                    // Last, so physics callbacks and WaitForFixedUpdate coroutines still see the
                    // step they are in.
                    children.Add(system);
                    node.subSystemList[i].subSystemList = children.ToArray();
                    return true;
                }

                if (AppendToFixedUpdate(ref node.subSystemList[i], system))
                    return true;
            }

            return false;
        }

        static bool RemoveMarker(ref PlayerLoopSystem node)
        {
            if (node.subSystemList == null)
                return false;

            bool removed = false;
            List<PlayerLoopSystem> kept = new List<PlayerLoopSystem>(node.subSystemList.Length);

            for (int i = 0; i < node.subSystemList.Length; i++)
            {
                if (node.subSystemList[i].type == typeof(FixedInputTickUpdate))
                {
                    removed = true;
                    continue;
                }

                PlayerLoopSystem child = node.subSystemList[i];
                if (RemoveMarker(ref child))
                    removed = true;

                kept.Add(child);
            }

            if (removed)
                node.subSystemList = kept.ToArray();

            return removed;
        }

        static bool FindMarker(PlayerLoopSystem node)
        {
            if (node.subSystemList == null)
                return false;

            foreach (PlayerLoopSystem child in node.subSystemList)
            {
                if (child.type == typeof(FixedInputTickUpdate) || FindMarker(child))
                    return true;
            }

            return false;
        }
    }
}
