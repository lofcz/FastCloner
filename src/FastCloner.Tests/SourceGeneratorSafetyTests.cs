using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;
using System;
using System.Threading;

namespace FastCloner.Tests.SourceGenerator
{
    // Define a class that uses the Source Generator
    [FastClonerClonable]
    public class SafetyTestClass
    {
        public CancellationTokenSource? Cts { get; set; }
        public WeakReference<string>? WeakRef { get; set; }
        public WeakReference? NonGenericWeakRef { get; set; }
        public System.Threading.Tasks.Task? Task { get; set; }
    }

    [TestFixture]
    public class SourceGeneratorSafetyTests
    {
        [Test]
        public void Verify_SourceGenerator_Uses_ReferenceCopy_For_SpecialTypes()
        {
            // Arrange
            SafetyTestClass source = new SafetyTestClass
            {
                Cts = new CancellationTokenSource(),
                WeakRef = new WeakReference<string>("test"),
                NonGenericWeakRef = new WeakReference("test"),
                Task = System.Threading.Tasks.Task.CompletedTask
            };

            // Act
            // This calls the Source Generated FastDeepClone() method
            SafetyTestClass clone = source.FastDeepClone();

            // Assert
            Assert.That(clone, Is.Not.SameAs(source));
            
            // CancellationTokenSource should be Reference Copy
            Assert.That(clone.Cts, Is.SameAs(source.Cts), "CTS should be reference copied by Source Generator");

            // WeakReference<T> should be Reference Copy
            Assert.That(clone.WeakRef, Is.SameAs(source.WeakRef), "WeakReference<T> should be reference copied by Source Generator");
            
            // WeakReference (non-generic) should be Reference Copy
            Assert.That(clone.NonGenericWeakRef, Is.SameAs(source.NonGenericWeakRef), "WeakReference should be reference copied by Source Generator");

            // Task should be Reference Copy
            Assert.That(clone.Task, Is.SameAs(source.Task), "Task should be reference copied by Source Generator");
        }
    }
}
