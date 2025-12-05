using System;
using System.Diagnostics.CodeAnalysis;
using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;

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


[TestFixture]
public class ContextTests
{
    [Test]
    public void Context_Should_Expose_Typed_Instance_Clone_Methods()
    {
        MyRegisteredType original = new MyRegisteredType { Name = "Instance", Value = 789 };
        MyCloningContext ctx = new MyCloningContext();
        MyRegisteredType clone = ctx.Clone(original); // Instance method on context returning typed object
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Name, Is.EqualTo("Instance"));
    }
    
    [Test]
    public void Context_Should_Clone_Registered_Types()
    {
        FastClonerContext ctx = new MyCloningContext();
        
        MyRegisteredType original = new MyRegisteredType { Name = "Test", Value = 123 };
        MyRegisteredType clone = (MyRegisteredType)ctx.Clone(original);
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Name, Is.EqualTo("Test"));
        Assert.That(clone.Value, Is.EqualTo(123));
        
        // Check IsHandled
        Assert.That(ctx.IsHandled(typeof(MyRegisteredType)), Is.True);
        Assert.That(ctx.IsHandled(typeof(string)), Is.False);
        
        // Check TryClone
        Assert.That(ctx.TryClone(original, out object? triedClone), Is.True);
        Assert.That(triedClone, Is.Not.SameAs(original));
        Assert.That(((MyRegisteredType)triedClone!).Name, Is.EqualTo("Test"));
    }

    [Test]
    public void Context_Should_Clone_Nested_Registered_Types()
    {
        FastClonerContext ctx = new MyCloningContext();
        MyOtherRegisteredType original = new MyOtherRegisteredType 
        { 
            Nested = new MyRegisteredType { Name = "Nested", Value = 456 } 
        };
        
        MyOtherRegisteredType clone = (MyOtherRegisteredType)ctx.Clone(original);
        
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Nested, Is.Not.SameAs(original.Nested));
        Assert.That(clone.Nested!.Name, Is.EqualTo("Nested"));
    }

    [Test]
    public void Context_Should_Handle_Complex_Circular_Dependencies()
    {
        CircularContext ctx = new CircularContext();
        
        // 1. Direct Cycle A <-> B
        NodeA a = new NodeA();
        NodeB b = new NodeB();
        a.B = b;
        b.A = a;
        
        NodeA cloneA = ctx.Clone(a);
        Assert.That(cloneA, Is.Not.SameAs(a));
        Assert.That(cloneA.B, Is.Not.SameAs(b));
        Assert.That(cloneA.B!.A, Is.SameAs(cloneA)); // Cycle preserved
        
        // 2. Self Cycle C -> C
        NodeC c = new NodeC();
        c.Self = c;
        
        NodeC cloneC = ctx.Clone(c);
        Assert.That(cloneC, Is.Not.SameAs(c));
        Assert.That(cloneC.Self, Is.SameAs(cloneC)); // Cycle preserved
        
        // 3. Lollipop Graph D -> E <-> F
        NodeD d = new NodeD();
        NodeE e = new NodeE();
        NodeF f = new NodeF();
        d.E = e;
        e.F = f;
        f.E = e;
        
        NodeD cloneD = ctx.Clone(d);
        Assert.That(cloneD, Is.Not.SameAs(d));
        Assert.That(cloneD.E, Is.Not.SameAs(e));
        Assert.That(cloneD.E!.F, Is.Not.SameAs(f));
        Assert.That(cloneD.E!.F!.E, Is.SameAs(cloneD.E)); // Cycle preserved
    }

    [Test]
    public void Context_Should_Clone_Class_Without_Public_Parameterless_Constructor()
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
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(original));
        // Read-only properties from constructor will be default values (not cloned)
        // Writable properties set after construction will be cloned
        Assert.That(clone.Description, Is.EqualTo("Test Description"));
        Assert.That(clone.AdditionalData, Is.EqualTo("Extra Data"));
    }

    [Test]
    public void Context_Should_Clone_Class_Without_Parameterless_Constructor_With_Circular_References()
    {
        // Arrange - Test that circular reference tracking works with FormatterServices
        NoParameterlessCtorCircularContext ctx = new NoParameterlessCtorCircularContext();
        ClassWithCircularRef original = new ClassWithCircularRef("Initial");
        original.Self = original; // Create circular reference
        
        // Act
        ClassWithCircularRef clone = (ClassWithCircularRef)ctx.Clone(original);
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Self, Is.SameAs(clone)); // Circular reference should be preserved
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
