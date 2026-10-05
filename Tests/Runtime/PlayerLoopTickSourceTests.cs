using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyGameDevTools.FixedInput.Tests
{
    /// <summary>The properties a unit test cannot prove.</summary>
    public class PlayerLoopTickSourceTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                    Object.Destroy(spawned);
            }

            _spawned.Clear();
            MyFixedTick.ResetToDefault();

            if (!PlayerLoopTickSource.IsInstalled)
                PlayerLoopTickSource.Reinstall();
        }

        T Spawn<T>(string name) where T : Component
        {
            GameObject go = new GameObject(name);
            _spawned.Add(go);
            return go.AddComponent<T>();
        }

        [Test]
        public void InstallsItself_WithNoSceneObjectAndNoExecutionOrder()
        {
            Assert.IsTrue(PlayerLoopTickSource.IsInstalled);
        }

        [Test]
        public void Reinstall_IsIdempotent_SoRepeatedCallsCannotStackDuplicates()
        {
            PlayerLoopTickSource.Reinstall();
            PlayerLoopTickSource.Reinstall();
            PlayerLoopTickSource.Reinstall();

            Assert.AreEqual(1, CountMarkersInPlayerLoop(), "Three installs must leave exactly one increment.");
        }

        [Test]
        public void Uninstall_ThenReinstall_LeavesExactlyOne()
        {
            PlayerLoopTickSource.Uninstall();
            Assert.AreEqual(0, CountMarkersInPlayerLoop());

            PlayerLoopTickSource.Reinstall();
            Assert.AreEqual(1, CountMarkersInPlayerLoop());
        }

        [UnityTest]
        public IEnumerator AdvancesExactlyOncePerFixedStep()
        {
            yield return new WaitForFixedUpdate();

            uint start = PlayerLoopTickSource.Instance.Tick;

            for (int step = 1; step <= 5; step++)
            {
                yield return new WaitForFixedUpdate();
                Assert.AreEqual(start + (uint)step, PlayerLoopTickSource.Instance.Tick, $"After {step} fixed steps.");
            }
        }

        [UnityTest]
        public IEnumerator PhysicsCallbacksObserveTheStepTheyAreIn()
        {
            // This is the property the install position exists for.
            PhysicsTickProbe probe = Spawn<PhysicsTickProbe>("Probe");
            BoxCollider probeCollider = probe.gameObject.AddComponent<BoxCollider>();
            probeCollider.isTrigger = true;
            // Dynamic, not kinematic: under the default contact pairs mode, Unity 6000.0 and 6000.3
            // report no trigger events between a kinematic body and a static collider.
            Rigidbody body = probe.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;

            // Spawned overlapping, so the trigger fires on the first physics step that runs.
            BoxCollider other = Spawn<BoxCollider>("Other");
            other.transform.position = probe.transform.position;

            for (int step = 0; step < 10 && !probe.TriggerFired; step++)
                yield return new WaitForFixedUpdate();

            Assert.IsTrue(probe.TriggerFired, "The trigger never fired, so this test proved nothing.");
            Assert.AreEqual(probe.FixedUpdateTick, probe.TriggerTick,
                "A physics callback during a step must read the same tick as a FixedUpdate writer in that step.");
        }

        [UnityTest]
        public IEnumerator AnUpdateWriterBindsToTheNextStep_AndIsConsumedExactlyOnce()
        {
            // Frame independence, the property a frame-rate sweep would otherwise find the hard
            // way.
            UpdateWriterFixedReader pair = Spawn<UpdateWriterFixedReader>("WriterReader");

            yield return new WaitForFixedUpdate();
            pair.Begin();

            for (int frame = 0; frame < 120; frame++)
                yield return null;

            pair.Stop();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.Greater(pair.PressesIssued, 0, "The writer never pressed, so this test proved nothing.");
            Assert.AreEqual(pair.PressesIssued, pair.PressesConsumed,
                "Every press the writer issued must be consumed exactly once, whatever the frame rate.");
            Assert.AreEqual(0, pair.DoubleConsumes, "No press may be consumed twice.");
        }

        [UnityTest]
        public IEnumerator AHealthyFixedUpdateReaderDoesNotTripTheEvictionCheck()
        {
            // A reader inside FixedUpdate runs before the end-of-phase increment, so it always sees
            // a full step of lag.
            QuietFixedReader reader = Spawn<QuietFixedReader>("QuietReader");

            for (int step = 0; step < 10; step++)
                yield return new WaitForFixedUpdate();

            Assert.Greater(reader.Reads, 0, "The reader never ran, so this test proved nothing.");
        }

        [UnityTest]
        public IEnumerator EvictionIsReported_RatherThanSilentlyFreezingTheTick()
        {
            // Simulates another package replacing the player loop with a snapshot that predates us.
            _ = MyFixedTick.Current;
            PlayerLoopTickSource.Uninstall();

            for (int step = 0; step < 4; step++)
                yield return new WaitForFixedUpdate();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("tick is frozen"));
            _ = MyFixedTick.Current;

            PlayerLoopTickSource.Reinstall();
        }

        [UnityTest]
        public IEnumerator ReinstallStopsTheReporting_RatherThanBlamingItselfForTheGapItJustClosed()
        {
            _ = MyFixedTick.Current;
            PlayerLoopTickSource.Uninstall();

            for (int step = 0; step < 4; step++)
                yield return new WaitForFixedUpdate();

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("tick is frozen"));
            _ = MyFixedTick.Current;

            PlayerLoopTickSource.Reinstall();

            // Any further error fails the test, which is the point: the repair must take effect at once.
            for (int step = 0; step < 5; step++)
            {
                yield return new WaitForFixedUpdate();
                _ = MyFixedTick.Current;
            }
        }

        [UnityTest]
        public IEnumerator AStalledEditorIsNotMistakenForAnEviction()
        {
            // A breakpoint, an asset import or a long editor hitch leaves the same time gap an
            // eviction does.
            _ = MyFixedTick.Current;
            Assert.IsTrue(PlayerLoopTickSource.IsInstalled);

            // Nothing drives the fixed phase while this blocks, so the gap grows exactly as it
            // would during a stall, with the system still sitting in the loop.
            System.Threading.Thread.Sleep(400);

            // Any error logged here fails the test.
            _ = MyFixedTick.Current;
            yield return null;
            _ = MyFixedTick.Current;
        }

        [UnityTest]
        public IEnumerator TheUnconsumedCheckRunsByItself_WithNobodyCallingIt()
        {
            // The edit-mode tests drive the check directly.
            FixedInputDiagnostics.ResetState();

            LonelyWriter writer = Spawn<LonelyWriter>("LonelyWriter");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("'Attack'.*never consumed"));

            for (int step = 0; step < 5; step++)
                yield return new WaitForFixedUpdate();

            FixedInputDiagnostics.Unregister(writer);
            FixedInputDiagnostics.ResetState();
        }

        static int CountMarkersInPlayerLoop()
        {
            return Count(UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop());

            static int Count(UnityEngine.LowLevel.PlayerLoopSystem node)
            {
                if (node.subSystemList == null)
                    return 0;

                int found = 0;
                foreach (UnityEngine.LowLevel.PlayerLoopSystem child in node.subSystemList)
                {
                    if (child.type == typeof(PlayerLoopTickSource.FixedInputTickUpdate))
                        found++;

                    found += Count(child);
                }

                return found;
            }
        }

        /// <summary>A writer with no reader, which is what a wrong execution order looks like from outside.</summary>
        class LonelyWriter : MonoBehaviour
        {
            public FixedInputEvent Attack;

            bool _pressed;

            void OnEnable() => FixedInputDiagnostics.Register(this);

            void OnDisable() => FixedInputDiagnostics.Unregister(this);

            void FixedUpdate()
            {
                if (_pressed)
                    return;

                _pressed = true;
                Attack.Set(MyFixedTick.Current);
            }
        }

        class QuietFixedReader : MonoBehaviour
        {
            public int Reads { get; private set; }

            void FixedUpdate()
            {
                _ = MyFixedTick.Current;
                Reads++;
            }
        }

        class PhysicsTickProbe : MonoBehaviour
        {
            public uint FixedUpdateTick { get; private set; }
            public uint TriggerTick { get; private set; }
            public bool TriggerFired { get; private set; }

            void FixedUpdate() => FixedUpdateTick = MyFixedTick.Current;

            void OnTriggerEnter(Collider other)
            {
                if (TriggerFired)
                    return;

                TriggerTick = MyFixedTick.Current;
                TriggerFired = true;
            }
        }

        /// <summary>
        /// A device-style writer in Update and a body-style reader in FixedUpdate, in one component
        /// so their execution order relative to each other is fixed.
        /// </summary>
        class UpdateWriterFixedReader : MonoBehaviour
        {
            public int PressesIssued { get; private set; }
            public int PressesConsumed { get; private set; }
            public int DoubleConsumes { get; private set; }

            FixedInputEvent _attack;
            bool _running;
            int _framesUntilNextPress;

            public void Begin()
            {
                _running = true;
                _framesUntilNextPress = 0;
            }

            public void Stop() => _running = false;

            void Update()
            {
                if (!_running)
                    return;

                // Press every few render frames, which at any sane frame rate means some presses
                // land in frames with no fixed step at all.
                if (_framesUntilNextPress-- > 0)
                    return;

                _framesUntilNextPress = 7;

                // Do not issue a press while one is still pending, or the two would collapse into
                // one and the count would be a lie rather than a finding.
                if (_attack.IsArmed)
                    return;

                _attack.Set(MyFixedTick.Current);
                PressesIssued++;
            }

            void FixedUpdate()
            {
                uint tick = MyFixedTick.Current;

                if (_attack.TryConsume(tick))
                {
                    PressesConsumed++;

                    if (_attack.TryConsume(tick))
                        DoubleConsumes++;
                }
            }
        }
    }
}
