using System.Text.RegularExpressions;
using MyGameDevTools.FixedInput.Entities;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyGameDevTools.FixedInput.Tests
{
    /// <summary>Only the ECS-specific parts.</summary>
    public class FixedInputEntitiesTests
    {
        /// <summary>
        /// An intent sitting behind padding and a nested struct, because a scan that only handles
        /// an intent at offset zero would pass a simpler fixture and still be wrong.
        /// </summary>
        struct DemoInput : IComponentData
        {
            public byte Leading;
            public Nested Inner;
            public FixedInputEvent Attack;

            public struct Nested
            {
                public int Filler;
                public FixedInputEvent Buried;
            }
        }

        World _world;

        [SetUp]
        public void SetUp()
        {
            FixedInputDiagnostics.ResetState();
            _world = new World("FixedInputTests");
        }

        [TearDown]
        public void TearDown()
        {
            if (_world is { IsCreated: true })
                _world.Dispose();

            FixedInputDiagnostics.ResetState();
        }

        FixedStepSimulationSystemGroup CreateFixedGroup()
        {
            FixedStepSimulationSystemGroup group = _world.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            group.AddSystemToUpdateList(_world.GetOrCreateSystem<FixedTickInitializationSystem>());
            group.AddSystemToUpdateList(_world.GetOrCreateSystem<FixedTickEntitiesSystem>());
            group.AddSystemToUpdateList(_world.GetOrCreateSystem<FixedInputEventEntitiesDiagnostics>());
            group.SortSystems();
            return group;
        }

        /// <summary>
        /// Runs exactly group updates, with the rate manager taken out so the count is
        /// deterministic rather than a function of wall-clock time.
        /// </summary>
        void Step(FixedStepSimulationSystemGroup group, int steps)
        {
            group.RateManager = null;

            for (int i = 0; i < steps; i++)
                group.Update();
        }

        [Test]
        public void TheIntentIsUnmanaged_SoItCanBeAComponentField()
        {
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<FixedInputEvent>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<DemoInput>());
        }

        [Test]
        public void TheTickSingletonIsCreatedAndAdvancesOncePerGroupUpdate()
        {
            FixedStepSimulationSystemGroup group = CreateFixedGroup();

            Step(group, 1);
            Assert.IsTrue(_world.EntityManager.CreateEntityQuery(typeof(FixedTickSingleton)).CalculateEntityCount() == 1,
                "The initialization system must create the singleton before anything reads it.");

            uint start = _world.EntityManager.CreateEntityQuery(typeof(FixedTickSingleton)).GetSingleton<FixedTickSingleton>().Value;

            Step(group, 5);

            uint end = _world.EntityManager.CreateEntityQuery(typeof(FixedTickSingleton)).GetSingleton<FixedTickSingleton>().Value;
            Assert.AreEqual(start + 5, end);
        }

        [Test]
        public void EntitiesTickSourceReportsTheSingletonThroughTheInterface()
        {
            FixedStepSimulationSystemGroup group = CreateFixedGroup();
            Step(group, 4);

            ITickSource source = new EntitiesTickSource(_world);
            uint expected = _world.EntityManager.CreateEntityQuery(typeof(FixedTickSingleton)).GetSingleton<FixedTickSingleton>().Value;

            Assert.AreEqual(expected, source.Tick);
        }

        [Test]
        public void TheScanFindsAnIntentBehindPaddingAndInsideANestedStruct()
        {
            // The whole reason these tests exist.
            FixedStepSimulationSystemGroup group = CreateFixedGroup();

            Entity entity = _world.EntityManager.CreateEntity(typeof(DemoInput));
            DemoInput input = default;
            input.Leading = 0xFF;
            input.Inner.Filler = int.MaxValue;
            input.Inner.Buried.Set(0);
            _world.EntityManager.SetComponentData(entity, input);

            LogAssert.Expect(LogType.Warning, new Regex(@"DemoInput\.Buried.*never consumed"));

            Step(group, 4);
        }

        [Test]
        public void AConsumedEntityIntentIsNotReported()
        {
            FixedStepSimulationSystemGroup group = CreateFixedGroup();

            Entity entity = _world.EntityManager.CreateEntity(typeof(DemoInput));
            DemoInput input = default;
            input.Attack.Set(0);
            input.Attack.TryConsume(0);
            input.Inner.Buried.Set(0);
            input.Inner.Buried.TryConsume(0);
            _world.EntityManager.SetComponentData(entity, input);

            Step(group, 6);
        }

        [Test]
        public void TheEntitiesDiagnosticNeedsNoRegistration()
        {
            // The managed path needs Register in OnEnable.
            FixedStepSimulationSystemGroup group = CreateFixedGroup();

            Entity entity = _world.EntityManager.CreateEntity(typeof(DemoInput));
            DemoInput input = default;
            input.Attack.Set(0);
            _world.EntityManager.SetComponentData(entity, input);

            LogAssert.Expect(LogType.Warning, new Regex(@"DemoInput\.Attack"));

            Step(group, 4);
        }

        [Test]
        public void DisablingDiagnosticsSilencesTheEntitiesPathToo()
        {
            FixedInputDiagnostics.Enabled = false;

            FixedStepSimulationSystemGroup group = CreateFixedGroup();

            Entity entity = _world.EntityManager.CreateEntity(typeof(DemoInput));
            DemoInput input = default;
            input.Attack.Set(0);
            _world.EntityManager.SetComponentData(entity, input);

            Step(group, 6);
        }
    }
}
