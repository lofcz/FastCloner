using System.Reflection;
using FastCloner.Code;

namespace FastCloner.Tests;

public class FastCloneStatePoolTests
{
    private const int MaxRetainedWorkQueueCapacity = 4096;

    private static readonly FieldInfo WorkItemsField =
        typeof(FastCloneState).GetField("workItems", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly Type WorkItemType =
        typeof(FastCloneState).GetNestedType("WorkItem", BindingFlags.NonPublic)!;

    private static readonly FieldInfo WorkItemFromField =
        WorkItemType.GetField("From", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly FieldInfo WorkItemToField =
        WorkItemType.GetField("To", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    private static readonly FieldInfo WorkItemTypeField =
        WorkItemType.GetField("Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 4)]
    [Arguments(true, 0)]
    [Arguments(true, 4)]
    public async Task CloneReferenceFreeArray_TracksArrayWithoutReservingPerElement(bool useStringElements, int existingReferences)
    {
        // Arrange
        FastCloneState state = FastCloneState.Rent();
        FieldInfo loopsField = typeof(FastCloneState).GetField("loops", BindingFlags.Instance | BindingFlags.NonPublic)!;
        loopsField.SetValue(state, null);
        Array original = useStringElements
            ? Enumerable.Repeat("value", 10000).ToArray()
            : Enumerable.Range(0, 10000).ToArray();

        try
        {
            for (int i = 0; i < existingReferences; i++)
                state.AddKnownRef(new object(), new object());

            // Act
            Array clone = original is string[] strings
                ? FastClonerGenerator.Clone1DimArraySafeInternal(strings, state)
                : FastClonerGenerator.Clone1DimArraySafeInternal((int[])original, state);
            object? loops = loopsField.GetValue(state);
            int capacity = loops is null ? 0 : (int)loops.GetType().GetProperty("Capacity")!.GetValue(loops)!;

            // Assert
            await Assert.That(clone).IsNotSameReferenceAs(original);
            await Assert.That(clone).IsEquivalentTo(original);
            await Assert.That(state.GetKnownRef(original)).IsSameReferenceAs(clone);
            await Assert.That(capacity).IsLessThanOrEqualTo(8);
        }
        finally
        {
            FastCloneState.Return(state);
        }
    }

    [Test]
    public async Task TryPop_Clears_Popped_WorkItem_References()
    {
        FastCloneState state = FastCloneState.Rent();
        object from = new object();
        object to = new object();

        try
        {
            state.EnqueueProcess(from, to, typeof(object));

            bool popped = state.TryPop(out object actualFrom, out object actualTo, out Type actualType);
            Array workItems = (Array)WorkItemsField.GetValue(state)!;
            object clearedSlot = workItems.GetValue(0)!;

            using (Assert.Multiple())
            {
                await Assert.That(popped).IsTrue();
                await Assert.That(actualFrom).IsSameReferenceAs(from);
                await Assert.That(actualTo).IsSameReferenceAs(to);
                await Assert.That(actualType).IsSameReferenceAs(typeof(object));
                await Assert.That(WorkItemFromField.GetValue(clearedSlot)).IsNull();
                await Assert.That(WorkItemToField.GetValue(clearedSlot)).IsNull();
                await Assert.That(WorkItemTypeField.GetValue(clearedSlot)).IsNull();
            }
        }
        finally
        {
            FastCloneState.Return(state);
        }
    }

    [Test]
    public async Task Return_Releases_Oversized_WorkQueue_Buffer()
    {
        FastCloneState state = FastCloneState.Rent();

        state.EnsureWorkQueueCapacity(MaxRetainedWorkQueueCapacity + 1);
        Array largeBuffer = (Array)WorkItemsField.GetValue(state)!;

        await Assert.That(largeBuffer.Length).IsGreaterThan(MaxRetainedWorkQueueCapacity);

        FastCloneState.Return(state);

        await Assert.That(WorkItemsField.GetValue(state)).IsNull();
    }
}
