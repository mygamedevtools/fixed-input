#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace MyGameDevTools.FixedInput.Entities
{
    /// <summary>Reports entity intents that nobody consumed.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(FixedTickEntitiesSystem))]
    public partial struct FixedInputEventEntitiesDiagnostics : ISystem
    {
        /// <summary>One field inside one component type.</summary>
        struct IntentField
        {
            public ComponentType Component;
            public int Offset;
            public int ComponentSize;
            public uint Window;
            public FixedString128Bytes Description;
        }

        NativeList<IntentField> _fields;
        EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            List<IntentField> found = ScanTypeManager();

            if (found.Count == 0)
            {
                // No component in the project holds an intent, so there is nothing to watch.
                state.Enabled = false;
                return;
            }

            _fields = new NativeList<IntentField>(found.Count, Allocator.Persistent);

            NativeList<ComponentType> any = new NativeList<ComponentType>(found.Count, Allocator.Temp);

            for (int i = 0; i < found.Count; i++)
            {
                _fields.Add(found[i]);

                // One component can hold several intents, and the query wants each type once.
                if (!any.Contains(found[i].Component))
                    any.Add(found[i].Component);
            }

            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAny(ref any)
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(ref state);

            any.Dispose();

            state.RequireForUpdate<FixedTickSingleton>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_fields.IsCreated)
                _fields.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!FixedInputDiagnostics.Enabled)
                return;

            uint tick = SystemAPI.GetSingleton<FixedTickSingleton>().Value;

            // Handles are per-update state, so build them once rather than once per chunk.
            NativeArray<DynamicComponentTypeHandle> handles = new NativeArray<DynamicComponentTypeHandle>(_fields.Length, Allocator.Temp);

            for (int f = 0; f < _fields.Length; f++)
                handles[f] = state.EntityManager.GetDynamicComponentTypeHandle(_fields[f].Component);

            EntityTypeHandle entityHandle = state.GetEntityTypeHandle();
            NativeArray<ArchetypeChunk> chunks = _query.ToArchetypeChunkArray(Allocator.Temp);

            foreach (ArchetypeChunk chunk in chunks)
            {
                for (int f = 0; f < _fields.Length; f++)
                {
                    IntentField field = _fields[f];
                    DynamicComponentTypeHandle handle = handles[f];

                    if (!chunk.Has(ref handle))
                        continue;

                    NativeArray<byte> raw = chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref handle, field.ComponentSize);
                    NativeArray<Entity> entities = chunk.GetNativeArray(entityHandle);

                    for (int e = 0; e < chunk.Count; e++)
                    {
                        FixedInputEvent value = ReadIntent(raw, e * field.ComponentSize + field.Offset);

                        if (!value.IsArmed)
                            continue;

                        uint age = unchecked(tick - value.SetTick);

                        // A future set tick underflows to an enormous age, which means an intent
                        // waiting for its step rather than one that was lost.
                        if (age <= field.Window || age > uint.MaxValue / 2)
                            continue;

                        Debug.LogWarning(
                            $"[FixedInput] '{field.Description}' on {entities[e].ToFixedString()} was set at tick {value.SetTick} " +
                            $"and never consumed (now tick {tick}, window {field.Window}).\n" +
                            "Either no system reads this field, or its reader is ordered before its writer in the same step.");
                    }
                }
            }

            chunks.Dispose();
            handles.Dispose();
        }

        static unsafe FixedInputEvent ReadIntent(NativeArray<byte> raw, int byteOffset)
        {
            byte* basePointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(raw);
            return UnsafeUtility.ReadArrayElement<FixedInputEvent>(basePointer + byteOffset, 0);
        }

        /// <summary>
        /// Finds every registered component type holding a FixedInputEvent, and where inside it
        /// sits.
        /// </summary>
        static List<IntentField> ScanTypeManager()
        {
            List<IntentField> found = new List<IntentField>();

            foreach (TypeManager.TypeInfo info in TypeManager.GetAllTypes())
            {
                if (info.Category != TypeManager.TypeCategory.ComponentData)
                    continue;

                Type type = info.Type;

                if (type == null || info.SizeInChunk <= 0)
                    continue;

                foreach ((FieldInfo field, int offset) in IntentFieldsOf(type))
                {
                    found.Add(new IntentField
                    {
                        Component = ComponentType.ReadOnly(info.TypeIndex),
                        Offset = offset,
                        ComponentSize = info.SizeInChunk,
                        Window = field.GetCustomAttribute<FixedInputWindowAttribute>()?.Window ?? FixedInputDiagnostics.DefaultWindow,
                        Description = new FixedString128Bytes($"{type.Name}.{field.Name}")
                    });
                }
            }

            return found;
        }

        /// <summary>
        /// Walks a component's fields, including nested structs, returning the byte offset of every
        /// relative to the start of the component.
        /// </summary>
        static IEnumerable<(FieldInfo field, int offset)> IntentFieldsOf(Type type, int baseOffset = 0, int depth = 0)
        {
            // Guards against a cycle that cannot exist in a value type but costs nothing to rule out.
            if (depth > 8)
                yield break;

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                int offset = baseOffset + UnsafeUtility.GetFieldOffset(field);

                if (field.FieldType == typeof(FixedInputEvent))
                {
                    yield return (field, offset);
                    continue;
                }

                if (!field.FieldType.IsValueType || field.FieldType.IsPrimitive || field.FieldType.IsEnum)
                    continue;

                foreach ((FieldInfo nested, int nestedOffset) in IntentFieldsOf(field.FieldType, offset, depth + 1))
                    yield return (nested, nestedOffset);
            }
        }
    }
}
#endif
