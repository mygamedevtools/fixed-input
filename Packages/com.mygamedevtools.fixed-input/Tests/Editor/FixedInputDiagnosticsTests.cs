using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyGameDevTools.FixedInput.Tests
{
    /// <summary>
    /// The diagnostics exist so that a wrong writer order announces itself instead of producing a
    /// character that silently ignores input, so what they do and do not report is the behavior.
    /// </summary>
    public class FixedInputDiagnosticsTests
    {
        class Surface
        {
            public FixedInputEvent Attack;

            [FixedInputWindow(5)]
            public FixedInputEvent Combo;
        }

        Surface _surface;

        [SetUp]
        public void SetUp()
        {
            FixedInputDiagnostics.ResetState();
            _surface = new Surface();
            FixedInputDiagnostics.Register(_surface);
        }

        [TearDown]
        public void TearDown()
        {
            FixedInputDiagnostics.Unregister(_surface);
            FixedInputDiagnostics.ResetState();
        }

        [Test]
        public void AnIntentNobodyConsumes_IsReportedAndNamed()
        {
            _surface.Attack.Set(100);

            // Still consumable during its own step, so nothing yet.
            FixedInputDiagnostics.CheckForUnconsumed(100);

            LogAssert.Expect(LogType.Warning, new Regex("'Attack'.*set at tick 100 and never consumed"));
            FixedInputDiagnostics.CheckForUnconsumed(101);
        }

        [Test]
        public void AConsumedIntent_IsNotReported()
        {
            _surface.Attack.Set(100);
            Assert.IsTrue(_surface.Attack.TryConsume(100));

            FixedInputDiagnostics.CheckForUnconsumed(101);
            FixedInputDiagnostics.CheckForUnconsumed(102);
        }

        [Test]
        public void ADeclaredWindow_DelaysTheReportUntilTheIntentIsActuallyLost()
        {
            _surface.Combo.Set(100);

            for (uint tick = 100; tick <= 105; tick++)
                FixedInputDiagnostics.CheckForUnconsumed(tick);

            LogAssert.Expect(LogType.Warning, new Regex("'Combo'.*window 5"));
            FixedInputDiagnostics.CheckForUnconsumed(106);
        }

        [Test]
        public void AnIntentBoundToAFutureStep_IsNotReported()
        {
            // What an Update-phase writer produces between the increment and the next step.
            _surface.Attack.Set(101);

            FixedInputDiagnostics.CheckForUnconsumed(100);
        }

        [Test]
        public void TheSameLossIsReportedOncePerIncident_NotOncePerStep()
        {
            _surface.Attack.Set(100);

            LogAssert.Expect(LogType.Warning, new Regex("'Attack'"));
            FixedInputDiagnostics.CheckForUnconsumed(101);

            // Would be twenty identical lines a second without the suppression.
            for (uint tick = 102; tick <= 120; tick++)
                FixedInputDiagnostics.CheckForUnconsumed(tick);
        }

        [Test]
        public void AfterTheIncidentEnds_TheNextOneIsReportedAgain()
        {
            _surface.Attack.Set(100);
            LogAssert.Expect(LogType.Warning, new Regex("set at tick 100"));
            FixedInputDiagnostics.CheckForUnconsumed(101);

            _surface.Attack.Clear();
            FixedInputDiagnostics.CheckForUnconsumed(102);

            // A recurring bug must keep being reported, or the suppression hides the thing it is
            // meant to surface.
            _surface.Attack.Set(200);
            LogAssert.Expect(LogType.Warning, new Regex("set at tick 200"));
            FixedInputDiagnostics.CheckForUnconsumed(201);
        }

        [Test]
        public void TwoDifferentWritersInOneStep_AreReportedWithBothCallSites()
        {
            FixedInputDiagnostics.NoteSet(_surface, nameof(Surface.Attack), 100);

            LogAssert.Expect(LogType.Error, new Regex("set twice during tick 100, by two different writers"));
            WriteFromSomewhereElse();

            void WriteFromSomewhereElse() => FixedInputDiagnostics.NoteSet(_surface, nameof(Surface.Attack), 100);
        }

        [Test]
        public void TwoWritesFromOneSiteInOneStep_ReadAsAnOverwrittenPress()
        {
            // What a writer in Update does when the frame rate outruns the fixed rate.
            for (int i = 0; i < 2; i++)
                FixedInputDiagnostics.NoteSet(_surface, nameof(Surface.Attack), 100);

            LogAssert.Expect(LogType.Warning, new Regex("set twice during tick 100 from the same place"));
        }

        [Test]
        public void OneWritePerStep_IsNotReported()
        {
            for (uint tick = 100; tick < 110; tick++)
                FixedInputDiagnostics.NoteSet(_surface, nameof(Surface.Attack), tick);
        }

        [Test]
        public void Disabled_ReportsNothing()
        {
            FixedInputDiagnostics.Enabled = false;

            _surface.Attack.Set(100);
            FixedInputDiagnostics.CheckForUnconsumed(200);

            FixedInputDiagnostics.NoteSet(_surface, nameof(Surface.Attack), 100);
            FixedInputDiagnostics.NoteSet(_surface, nameof(Surface.Attack), 100);
        }

        [Test]
        public void AnUnregisteredSurface_IsNoLongerWatched()
        {
            FixedInputDiagnostics.Unregister(_surface);

            _surface.Attack.Set(100);
            FixedInputDiagnostics.CheckForUnconsumed(200);
        }
    }
}
