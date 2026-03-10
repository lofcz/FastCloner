using FastCloner.SourceGenerator.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FastCloner.Tests.SourceGenerator
{
    // Define a class that uses the Source Generator
    [FastClonerClonable]
    public class SafetyTestClass
    {
        public CancellationTokenSource? Cts { get; set; }
        public WeakReference<string>? WeakRef { get; set; }
        public WeakReference? NonGenericWeakRef { get; set; }
        public Task? Task { get; set; }
    }
    public class SourceGeneratorSafetyTests
    {
        [Test]
        public async Task Verify_SourceGenerator_Uses_ReferenceCopy_For_SpecialTypes()
        {
            // Arrange
            SafetyTestClass source = new SafetyTestClass
            {
                Cts = new CancellationTokenSource(),
                WeakRef = new WeakReference<string>("test"),
                NonGenericWeakRef = new WeakReference("test"),
                Task = Task.CompletedTask
            };

            // Act
            // This calls the Source Generated FastDeepClone() method
            SafetyTestClass clone = source.FastDeepClone();

            // Assert
            await Assert.That(clone).IsNotSameReferenceAs(source);

            // CancellationTokenSource should be Reference Copy
            await Assert.That(clone.Cts).IsSameReferenceAs(source.Cts).Because("CTS should be reference copied by Source Generator");

            // WeakReference<T> should be Reference Copy
            await Assert.That(clone.WeakRef).IsSameReferenceAs(source.WeakRef).Because("WeakReference<T> should be reference copied by Source Generator");

            // WeakReference (non-generic) should be Reference Copy
            await Assert.That(clone.NonGenericWeakRef).IsSameReferenceAs(source.NonGenericWeakRef).Because("WeakReference should be reference copied by Source Generator");

            // Task should be Reference Copy
            await Assert.That(ReferenceEquals(clone.Task, source.Task)).IsTrue().Because("Task should be reference copied by Source Generator");
        }
    }
}