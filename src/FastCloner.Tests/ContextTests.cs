using System;
using System.Diagnostics.CodeAnalysis;
using FastCloner.SourceGenerator.Shared;
using System.Threading.Tasks;

namespace FastCloner.Tests;

public class MyRegisteredType
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class MyOtherRegisteredType
{
    public MyRegisteredType? Nested { get; set; }
}

[FastClonerRegister(typeof(MyRegisteredType), typeof(MyOtherRegisteredType))]
public partial class MyCloningContext : FastClonerContext
{
}
public class ContextTests
{
    [Test]
    public async Task Context_Should_Expose_Typed_Instance_Clone_Methods()
    {
        MyRegisteredType original = new MyRegisteredType { Name = "Instance", Value = 789 };
        MyCloningContext ctx = new MyCloningContext();
        MyRegisteredType clone = ctx.Clone(original); // Instance method on context returning typed object
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsEqualTo("Instance");
    }
    
    [Test]
    public async Task Context_Should_Clone_Registered_Types()
    {
        FastClonerContext ctx = new MyCloningContext();
        
        MyRegisteredType original = new MyRegisteredType { Name = "Test", Value = 123 };
        MyRegisteredType clone = (MyRegisteredType)ctx.Clone(original);
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsEqualTo("Test");
        await Assert.That(clone.Value).IsEqualTo(123);

        // Check IsHandled
        await Assert.That(ctx.IsHandled(typeof(MyRegisteredType))).IsTrue();
        await Assert.That(ctx.IsHandled(typeof(string))).IsFalse();

        // Check TryClone
        await Assert.That(ctx.TryClone(original, out object? triedClone)).IsTrue();
        await Assert.That(triedClone).IsNotSameReferenceAs(original);
        await Assert.That(((MyRegisteredType)triedClone!).Name).IsEqualTo("Test");
    }

    [Test]
    public async Task Context_Should_Clone_Nested_Registered_Types()
    {
        FastClonerContext ctx = new MyCloningContext();
        MyOtherRegisteredType original = new MyOtherRegisteredType 
        { 
            Nested = new MyRegisteredType { Name = "Nested", Value = 456 } 
        };
        
        MyOtherRegisteredType clone = (MyOtherRegisteredType)ctx.Clone(original);
        
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Nested).IsNotSameReferenceAs(original.Nested);
        await Assert.That(clone.Nested!.Name).IsEqualTo("Nested");
    }

    [Test]
    public async Task Context_Should_Handle_Complex_Circular_Dependencies()
    {
        CircularContext ctx = new CircularContext();
        
        // 1. Direct Cycle A <-> B
        NodeA a = new NodeA();
        NodeB b = new NodeB();
        a.B = b;
        b.A = a;
        
        NodeA cloneA = ctx.Clone(a);
        await Assert.That(cloneA).IsNotSameReferenceAs(a);
        await Assert.That(cloneA.B).IsNotSameReferenceAs(b);
        await Assert.That(cloneA.B!.A).IsSameReferenceAs(cloneA); // Cycle preserved

        // 2. Self Cycle C -> C
        NodeC c = new NodeC();
        c.Self = c;
        
        NodeC cloneC = ctx.Clone(c);
        await Assert.That(cloneC).IsNotSameReferenceAs(c);
        await Assert.That(cloneC.Self).IsSameReferenceAs(cloneC); // Cycle preserved

        // 3. Lollipop Graph D -> E <-> F
        NodeD d = new NodeD();
        NodeE e = new NodeE();
        NodeF f = new NodeF();
        d.E = e;
        e.F = f;
        f.E = e;
        
        NodeD cloneD = ctx.Clone(d);
        await Assert.That(cloneD).IsNotSameReferenceAs(d);
        await Assert.That(cloneD.E).IsNotSameReferenceAs(e);
        await Assert.That(cloneD.E!.F).IsNotSameReferenceAs(f);
        await Assert.That(cloneD.E!.F!.E).IsSameReferenceAs(cloneD.E); // Cycle preserved
    }

    [Test]
    public async Task Context_Should_Clone_Class_Without_Public_Parameterless_Constructor()
    {
        // Arrange - ClassWithoutParameterlessCtor requires a parameter in its constructor
        // Note: Read-only properties set in constructor (Name, Value) won't be cloned since
        // FormatterServices.GetUninitializedObject() doesn't call the constructor.
        // Only writable properties set after construction will be cloned.
        NoParameterlessCtorContext ctx = new NoParameterlessCtorContext();
        ClassWithoutParameterlessCtor original = ClassWithoutParameterlessCtor.Create("TestName", 42);
        original.Description = "Test Description";
        original.AdditionalData = "Extra Data";
        
        // Act - Should use FormatterServices.GetUninitializedObject internally
        ClassWithoutParameterlessCtor clone = (ClassWithoutParameterlessCtor)ctx.Clone(original);
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        // Read-only properties from constructor will be default values (not cloned)
        // Writable properties set after construction will be cloned
        await Assert.That(clone.Description).IsEqualTo("Test Description");
        await Assert.That(clone.AdditionalData).IsEqualTo("Extra Data");
    }

    [Test]
    public async Task Context_Should_Clone_Class_Without_Parameterless_Constructor_With_Circular_References()
    {
        // Arrange - Test that circular reference tracking works with FormatterServices
        NoParameterlessCtorCircularContext ctx = new NoParameterlessCtorCircularContext();
        ClassWithCircularRef original = new ClassWithCircularRef("Initial");
        original.Self = original; // Create circular reference
        
        // Act
        ClassWithCircularRef clone = (ClassWithCircularRef)ctx.Clone(original);
        
        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Self).IsSameReferenceAs(clone); // Circular reference should be preserved
    }
}

public class NodeA { public NodeB? B { get; set; } }
public class NodeB { public NodeA? A { get; set; } }

public class NodeC { public NodeC? Self { get; set; } }

public class NodeD { public NodeE? E { get; set; } }
public class NodeE { public NodeF? F { get; set; } }
public class NodeF { public NodeE? E { get; set; } }

[FastClonerRegister(typeof(NodeA), typeof(NodeB), typeof(NodeC), typeof(NodeD), typeof(NodeE), typeof(NodeF))]
public partial class CircularContext : FastClonerContext {}

// Class without public parameterless constructor - requires constructor parameters
public class ClassWithoutParameterlessCtor
{
    // Read-only properties set in constructor - these won't be cloned
    // since FormatterServices.GetUninitializedObject() doesn't call the constructor
    public string? Name { get; }
    public int Value { get; }
    
    // Writable properties - these will be cloned
    public string Description { get; set; } = string.Empty;
    public string? AdditionalData { get; set; }

    // Only constructor requires parameters
    public ClassWithoutParameterlessCtor(string name, int value)
    {
        Name = name;
        Value = value;
    }

    // Factory method for convenience
    public static ClassWithoutParameterlessCtor Create(string name, int value)
    {
        return new ClassWithoutParameterlessCtor(name, value);
    }
}

[FastClonerRegister(typeof(ClassWithoutParameterlessCtor))]
public partial class NoParameterlessCtorContext : FastClonerContext {}

// Class without parameterless constructor that can have circular references
public class ClassWithCircularRef
{
    // Read-only property set in constructor - won't be cloned
    public string? Name { get; }
    // Writable property - will be cloned
    public ClassWithCircularRef? Self { get; set; }

    public ClassWithCircularRef(string name)
    {
        Name = name;
    }
}

[FastClonerRegister(typeof(ClassWithCircularRef))]
public partial class NoParameterlessCtorCircularContext : FastClonerContext {}