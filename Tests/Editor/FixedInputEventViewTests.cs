using MyGameDevTools.FixedInput.Editor;
using NUnit.Framework;

namespace MyGameDevTools.FixedInput.Tests
{
    /// <summary>
    /// The row is what someone stares at while wondering why a character ignored a press, so its
    /// wording is worth pinning down.
    /// </summary>
    public class FixedInputEventViewTests
    {
        static string Render(bool armed, uint setTick, uint currentTick, bool live, uint window = 0)
        {
            FixedInputEventView view = new FixedInputEventView("Attack");
            view.Refresh(armed, setTick, currentTick, live, window);
            return view.StateText;
        }

        [Test]
        public void Idle_WhenNotArmed()
        {
            Assert.AreEqual("○  idle", Render(armed: false, setTick: 0, currentTick: 0, live: true));
        }

        [Test]
        public void InEditMode_ShowsStateRatherThanAnInventedAge()
        {
            // No tick runs in edit mode, so an age would be fiction.
            Assert.AreEqual("●  armed @ tick 1234", Render(armed: true, setTick: 1234, currentTick: 0, live: false));
        }

        [Test]
        public void InPlayMode_ShowsAgeInTicks()
        {
            Assert.AreEqual("●  set this tick", Render(armed: true, setTick: 100, currentTick: 100, live: true));
            Assert.AreEqual("●  set this tick   (window 5)", Render(armed: true, setTick: 100, currentTick: 100, live: true, window: 5));
            Assert.AreEqual("●  set 1 tick ago   (window 5)", Render(armed: true, setTick: 100, currentTick: 101, live: true, window: 5));
            Assert.AreEqual("●  set 3 ticks ago   (window 5)", Render(armed: true, setTick: 100, currentTick: 103, live: true, window: 5));
        }

        [Test]
        public void PastItsWindow_ReadsAsLapsedRatherThanLive()
        {
            Assert.AreEqual("○  lapsed, set 6 ticks ago   (window 5)", Render(armed: true, setTick: 100, currentTick: 106, live: true, window: 5));
        }

        [Test]
        public void SetForAFutureStep_ReadsAsPending_NotAsFourBillionTicksAgo()
        {
            // The normal state for an Update-phase writer between the increment and the next step.
            Assert.AreEqual("●  pending next step", Render(armed: true, setTick: 101, currentTick: 100, live: true));
        }

        [Test]
        public void AcrossTheWrap_TheAgeIsStillSmall()
        {
            Assert.AreEqual("●  set 2 ticks ago   (window 5)", Render(armed: true, setTick: uint.MaxValue - 1, currentTick: 0, live: true, window: 5));
        }
    }
}
