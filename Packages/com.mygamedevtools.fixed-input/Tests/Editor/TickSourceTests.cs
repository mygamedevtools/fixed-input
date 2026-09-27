using NUnit.Framework;

namespace MyGameDevTools.FixedInput.Tests
{
    public class ManualTickSourceTests
    {
        [Test]
        public void StartsAtZero_AndAdvancesOneStepAtATime()
        {
            ManualTickSource ticks = new ManualTickSource();

            Assert.AreEqual(0u, ticks.Tick);
            ticks.Advance();
            Assert.AreEqual(1u, ticks.Tick);
        }

        [Test]
        public void CanStartAndJumpAnywhere_SoWrapBehaviourIsTestableWithoutCountingToFourBillion()
        {
            ManualTickSource ticks = new ManualTickSource(uint.MaxValue);
            Assert.AreEqual(uint.MaxValue, ticks.Tick);

            ticks.Advance();
            Assert.AreEqual(0u, ticks.Tick, "The counter wraps rather than throwing.");

            ticks.SetTick(500);
            ticks.Advance(10);
            Assert.AreEqual(510u, ticks.Tick);
        }

        [Test]
        public void DrivesAFullIntentLifecycle_WithNoEngineInvolved()
        {
            ManualTickSource ticks = new ManualTickSource();
            FixedInputEvent attack = default;

            attack.Set(ticks.Tick);
            Assert.IsTrue(attack.TryConsume(ticks.Tick));

            attack.Set(ticks.Tick);
            ticks.Advance();
            Assert.IsFalse(attack.TryConsume(ticks.Tick), "An intent does not survive into the next step.");
        }
    }

    public class MyFixedTickTests
    {
        [TearDown]
        public void TearDown() => MyFixedTick.ResetToDefault();

        [Test]
        public void OutsidePlayMode_ReportsASourceThatNeverAdvances()
        {
            // An inspector drawing an intent in edit mode must get a defined answer rather than
            // whatever a previous play session left behind.
            Assert.AreEqual(0u, MyFixedTick.Current);
            Assert.IsFalse(MyFixedTick.HasCustomSource);
        }

        [Test]
        public void ASuppliedSourceBecomesAuthoritative()
        {
            ManualTickSource ticks = new ManualTickSource(42);
            MyFixedTick.Source = ticks;

            Assert.IsTrue(MyFixedTick.HasCustomSource);
            Assert.AreEqual(42u, MyFixedTick.Current);

            ticks.Advance();
            Assert.AreEqual(43u, MyFixedTick.Current);
        }

        [Test]
        public void ResetToDefault_DropsASuppliedSource()
        {
            MyFixedTick.Source = new ManualTickSource(42);
            MyFixedTick.ResetToDefault();

            Assert.IsFalse(MyFixedTick.HasCustomSource);
            Assert.AreEqual(0u, MyFixedTick.Current, "Back to the edit-mode source, not the leftover manual one.");
        }
    }
}
