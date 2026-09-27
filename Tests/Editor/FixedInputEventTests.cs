using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace MyGameDevTools.FixedInput.Tests
{
    /// <summary>
    /// takes plain uint values and never sees a tick source, so every one of these runs with no
    /// scene, no play mode and no engine loop.
    /// </summary>
    public class FixedInputEventTests
    {
        [Test]
        public void Default_IsNeverVisible_EvenAtTickZero()
        {
            FixedInputEvent input = default;

            Assert.IsFalse(input.IsArmed);
            Assert.IsFalse(input.Peek(0), "A default intent must not be visible at tick 0, where its zeroed tick field would otherwise match.");
            Assert.IsFalse(input.Peek(0, window: 10));
            Assert.IsFalse(input.TryConsume(0));
        }

        [Test]
        public void Set_IsVisibleOnlyOnItsOwnTick()
        {
            FixedInputEvent input = default;
            input.Set(100);

            Assert.IsFalse(input.Peek(99), "The step before must not see it.");
            Assert.IsTrue(input.Peek(100));
            Assert.IsFalse(input.Peek(101), "It expires by arithmetic, with nothing clearing it.");
        }

        [Test]
        public void Set_InTheFuture_IsNotVisibleYet()
        {
            // An Update-phase writer stamps the step about to run, so a reader still inside the
            // previous step must not see it.
            FixedInputEvent input = default;
            input.Set(101);

            Assert.IsFalse(input.Peek(100));
            Assert.IsFalse(input.Peek(100, window: 10), "A window extends forward from the set tick, never backward.");
            Assert.IsTrue(input.Peek(101));
        }

        [Test]
        public void Buffered_SurvivesItsWindowAndExpiresAfter()
        {
            FixedInputEvent input = default;
            input.Set(100);

            for (uint tick = 100; tick <= 105; tick++)
                Assert.IsTrue(input.Peek(tick, window: 5), $"Tick {tick} is inside the window.");

            Assert.IsFalse(input.Peek(106, window: 5), "One step past the window is outside it.");
        }

        [Test]
        public void Window_IsPerRead_NotAPropertyOfTheIntent()
        {
            // The whole reason the window is an argument: one press, two readers, different leniency.
            FixedInputEvent input = default;
            input.Set(100);

            Assert.IsFalse(input.Peek(103), "The strict reader has already missed it.");
            Assert.IsTrue(input.Peek(103, window: 5), "The lenient reader still sees the same press.");
        }

        [Test]
        public void Peek_DoesNotConsume()
        {
            FixedInputEvent input = default;
            input.Set(100);

            Assert.IsTrue(input.Peek(100));
            Assert.IsTrue(input.Peek(100), "Peeking is what conditions do, and conditions run repeatedly.");
            Assert.IsTrue(input.TryConsume(100), "A peeked intent is still there to act on.");
        }

        [Test]
        public void Consumed_IsInvisibleToASecondReader()
        {
            FixedInputEvent input = default;
            input.Set(100);

            Assert.IsTrue(input.TryConsume(100));
            Assert.IsFalse(input.Peek(100), "Two readers must not both act on one press.");
            Assert.IsFalse(input.TryConsume(100));
        }

        [Test]
        public void Consumed_StaysConsumedAcrossAWindow()
        {
            // Without consumption a buffered intent would fire once per step for the whole window.
            FixedInputEvent input = default;
            input.Set(100);

            Assert.IsTrue(input.TryConsume(100, window: 5));

            for (uint tick = 100; tick <= 105; tick++)
                Assert.IsFalse(input.Peek(tick, window: 5), $"Tick {tick} must not re-fire a consumed intent.");
        }

        [Test]
        public void TryConsume_OutsideTheWindow_DoesNotDisarm()
        {
            FixedInputEvent input = default;
            input.Set(100);

            Assert.IsFalse(input.TryConsume(200), "Far past its step, so nothing to consume.");
            Assert.IsTrue(input.IsArmed, "A failed consume must not eat the intent.");
            Assert.IsTrue(input.Peek(100), "And the intent is still there for a reader on the right step.");
        }

        [Test]
        public void Clear_DisarmsWithoutActing()
        {
            FixedInputEvent input = default;
            input.Set(100);
            input.Clear();

            Assert.IsFalse(input.IsArmed);
            Assert.IsFalse(input.Peek(100));
        }

        [Test]
        public void Set_AgainRebindsToTheNewTick()
        {
            FixedInputEvent input = default;
            input.Set(100);
            input.Set(101);

            Assert.IsFalse(input.Peek(100), "The old binding is gone.");
            Assert.IsTrue(input.Peek(101));
        }

        [Test]
        public void SetJustBeforeTheWrap_IsStillVisibleAcrossIt()
        {
            // uint at 50 Hz wraps after about 2.7 years of continuous runtime.
            FixedInputEvent input = default;
            input.Set(uint.MaxValue - 1);

            Assert.IsTrue(input.Peek(uint.MaxValue - 1));
            Assert.IsTrue(input.Peek(uint.MaxValue, window: 5));
            Assert.IsTrue(input.Peek(0, window: 5), "Tick 0 is two steps after uint.MaxValue - 1.");
            Assert.IsTrue(input.Peek(3, window: 5));
            Assert.IsFalse(input.Peek(4, window: 5), "Five steps on from the set tick is still outside a window of five.");
        }

        [Test]
        public void ConsumeAcrossTheWrap_Works()
        {
            FixedInputEvent input = default;
            input.Set(uint.MaxValue);

            Assert.IsTrue(input.TryConsume(1, window: 2));
            Assert.IsFalse(input.IsArmed);
        }

        [Test]
        public void IsUnmanaged_SoItCanLiveInAComponentDataOrAJob()
        {
            // Guards the layout-invariance rule, so no managed field is ever added.
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<FixedInputEvent>());
        }

        [Test]
        public void Equality_TreatsEveryDisarmedValueAsEqual()
        {
            FixedInputEvent neverSet = default;

            FixedInputEvent setThenCleared = default;
            setThenCleared.Set(500);
            setThenCleared.Clear();

            Assert.AreEqual(neverSet, setThenCleared, "A disarmed intent's tick is meaningless, so the two are the same value.");
            Assert.AreEqual(neverSet.GetHashCode(), setThenCleared.GetHashCode());
        }
    }
}
