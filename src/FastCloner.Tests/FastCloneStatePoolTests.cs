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
