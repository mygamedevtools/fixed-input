using NUnit.Framework;

namespace MyGameDevTools.FixedInput.Samples.Tests
{
    /// <summary>
    /// The panel's timeline is only worth looking at if its classification is right, and one sample
    /// per step has to reconstruct writes that were already consumed by the time it ran.
    /// </summary>
    public class IntentTrackTests
    {
        static IntentTrack Track(uint window = 0, int capacity = 8) => new IntentTrack("Attack", window, capacity);

        [Test]
        public void AnUntouchedIntentReadsAsQuiet()
        {
            IntentTrack track = Track();
            FixedInputEvent intent = default;

            for (uint tick = 0; tick < 4; tick++)
                track.Sample(intent, tick);

            for (int back = 0; back < 4; back++)
                Assert.AreEqual(IntentActivity.Quiet, track.ActivityAt(back));
        }

        [Test]
        public void AFixedUpdateWriterAndReaderInOneStep_StillLeaveATrace()
        {
            // The case a naive armed-flag sampler misses entirely, because nothing is armed by the close.
            IntentTrack track = Track();
            FixedInputEvent intent = default;

            track.Sample(intent, 100);

            intent.Set(101);
            Assert.IsTrue(intent.TryConsume(101));
            track.Sample(intent, 101);

            Assert.AreEqual(IntentActivity.SetAndConsumed, track.ActivityAt(0));
            Assert.AreEqual((101u, 101u, 0L), track.LastConsume);
        }

        [Test]
        public void AnUpdateWriterShowsAsSetThenConsumedOnTheNextStep()
        {
            IntentTrack track = Track();
            FixedInputEvent intent = default;

            // Written during Update, so it binds to the step about to run.
            intent.Set(101);
            track.Sample(intent, 100);
            Assert.AreEqual(IntentActivity.Set, track.ActivityAt(0), "Bound to a future step, so waiting rather than ancient.");

            Assert.IsTrue(intent.TryConsume(101));
            track.Sample(intent, 101);
            Assert.AreEqual(IntentActivity.Consumed, track.ActivityAt(0));
            Assert.AreEqual(0L, track.LastConsume.age, "Consumed on the very step it was bound to.");
        }

        [Test]
        public void ABufferedIntentWaitsThroughItsWindow()
        {
            IntentTrack track = Track(window: 5, capacity: 16);
            FixedInputEvent intent = default;

            intent.Set(100);
            track.Sample(intent, 100);
            Assert.AreEqual(IntentActivity.Set, track.ActivityAt(0));

            for (uint tick = 101; tick <= 104; tick++)
                track.Sample(intent, tick);

            Assert.AreEqual(IntentActivity.Waiting, track.ActivityAt(0));

            Assert.IsTrue(intent.TryConsume(105, window: 5));
            track.Sample(intent, 105);

            Assert.AreEqual(IntentActivity.Consumed, track.ActivityAt(0));
            Assert.AreEqual(5L, track.LastConsume.age, "Five steps late, which is exactly what the window bought.");
        }

        [Test]
        public void AnIntentPastItsWindowReadsAsExpired()
        {
            // Nothing clears an intent, so one nobody took stays armed forever.
            IntentTrack track = Track(window: 2, capacity: 16);
            FixedInputEvent intent = default;

            intent.Set(100);
            track.Sample(intent, 100);
            track.Sample(intent, 101);
            track.Sample(intent, 102);
            Assert.AreEqual(IntentActivity.Waiting, track.ActivityAt(0));

            track.Sample(intent, 103);
            Assert.AreEqual(IntentActivity.Expired, track.ActivityAt(0));
        }

        [Test]
        public void ExpiryIsMarkedOnce_NotOnEveryStepAfterwards()
        {
            // Nothing clears a lapsed intent, so it stays armed indefinitely.
            IntentTrack track = Track(window: 2, capacity: 16);
            FixedInputEvent intent = default;

            intent.Set(100);
            track.Sample(intent, 100);
            track.Sample(intent, 101);
            track.Sample(intent, 102);
            track.Sample(intent, 103);
            Assert.AreEqual(IntentActivity.Expired, track.ActivityAt(0), "The step the window lapses.");

            for (uint tick = 104; tick <= 112; tick++)
            {
                track.Sample(intent, tick);
                Assert.AreEqual(IntentActivity.Quiet, track.ActivityAt(0), $"Tick {tick} is the same dead press, not a new one.");
            }
        }

        [Test]
        public void AFreshPressCanExpireAgain()
        {
            IntentTrack track = Track(window: 1, capacity: 16);
            FixedInputEvent intent = default;

            intent.Set(100);
            track.Sample(intent, 100);
            track.Sample(intent, 101);
            track.Sample(intent, 102);
            Assert.AreEqual(IntentActivity.Expired, track.ActivityAt(0));

            track.Sample(intent, 103);
            Assert.AreEqual(IntentActivity.Quiet, track.ActivityAt(0));

            // A new press is a new incident, and its own expiry is worth seeing.
            intent.Set(104);
            track.Sample(intent, 104);
            track.Sample(intent, 105);
            track.Sample(intent, 106);
            Assert.AreEqual(IntentActivity.Expired, track.ActivityAt(0));
        }

        [Test]
        public void HistoryIsOrderedNewestFirst()
        {
            IntentTrack track = Track(capacity: 8);
            FixedInputEvent intent = default;

            track.Sample(intent, 10);

            intent.Set(11);
            track.Sample(intent, 11);

            intent.TryConsume(11);
            track.Sample(intent, 12);

            Assert.AreEqual(IntentActivity.Consumed, track.ActivityAt(0));
            Assert.AreEqual(IntentActivity.Set, track.ActivityAt(1));
            Assert.AreEqual(IntentActivity.Quiet, track.ActivityAt(2));
            Assert.AreEqual(12u, track.NewestTick);
            Assert.AreEqual(10u, track.OldestTick);
        }

        [Test]
        public void TheRingKeepsTheNewestStepsAndDropsTheOldest()
        {
            IntentTrack track = Track(capacity: 4);
            FixedInputEvent intent = default;

            for (uint tick = 0; tick < 10; tick++)
                track.Sample(intent, tick);

            Assert.AreEqual(4, track.Count);
            Assert.AreEqual(10, track.TotalSamples);
            Assert.AreEqual(9u, track.NewestTick);
            Assert.AreEqual(6u, track.OldestTick, "Four steps kept, ending at nine.");
        }

        [Test]
        public void RepeatedPressesEachRegister()
        {
            IntentTrack track = Track(capacity: 8);
            FixedInputEvent intent = default;

            track.Sample(intent, 9);

            intent.Set(10);
            intent.TryConsume(10);
            track.Sample(intent, 10);

            intent.Set(11);
            intent.TryConsume(11);
            track.Sample(intent, 11);

            Assert.AreEqual(IntentActivity.SetAndConsumed, track.ActivityAt(0));
            Assert.AreEqual(IntentActivity.SetAndConsumed, track.ActivityAt(1), "A second press must not be mistaken for the first one lingering.");
        }

        [Test]
        public void TheFirstSampleOnlyEstablishesABaseline()
        {
            // There is nothing to compare a set tick against yet, so a track that starts watching a
            // surface already in use misses at most one press.
            IntentTrack track = Track(capacity: 8);
            FixedInputEvent intent = default;

            intent.Set(10);
            intent.TryConsume(10);
            track.Sample(intent, 10);

            Assert.AreEqual(IntentActivity.Quiet, track.ActivityAt(0));

            intent.Set(11);
            intent.TryConsume(11);
            track.Sample(intent, 11);

            Assert.AreEqual(IntentActivity.SetAndConsumed, track.ActivityAt(0));
        }

        [Test]
        public void ClassificationSurvivesTheTickWrap()
        {
            IntentTrack track = Track(window: 3, capacity: 8);
            FixedInputEvent intent = default;

            intent.Set(uint.MaxValue - 1);
            track.Sample(intent, uint.MaxValue - 1);
            Assert.AreEqual(IntentActivity.Set, track.ActivityAt(0));

            track.Sample(intent, uint.MaxValue);
            Assert.AreEqual(IntentActivity.Waiting, track.ActivityAt(0));

            Assert.IsTrue(intent.TryConsume(0, window: 3));
            track.Sample(intent, 0);
            Assert.AreEqual(IntentActivity.Consumed, track.ActivityAt(0));
            Assert.AreEqual(2L, track.LastConsume.age);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            IntentTrack track = Track(capacity: 8);
            FixedInputEvent intent = default;

            intent.Set(10);
            track.Sample(intent, 10);
            track.Clear();

            Assert.AreEqual(0, track.Count);
            Assert.AreEqual(0, track.TotalSamples);
            Assert.AreEqual(-1L, track.LastConsume.age);
        }
    }
}
