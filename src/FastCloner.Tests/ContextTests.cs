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

    [Test]
    public async Task Context_Should_Clone_Registered_Type_With_List_Member()
    {
        Issue56ListContext ctx = new Issue56ListContext();
        Issue56Address berlin = new Issue56Address { City = "Berlin" };
        Issue56Customer original = new Issue56Customer
        {
            Name = "Jane",
            Address = berlin,
            PreviousAddresses = [berlin, new Issue56Address { City = "Munich" }]
        };

        Issue56Customer clone = ctx.Clone(original)!;

        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsEqualTo("Jane");
        await Assert.That(clone.Address).IsNotSameReferenceAs(original.Address);
        await Assert.That(clone.Address!.City).IsEqualTo("Berlin");
        await Assert.That(clone.PreviousAddresses).IsNotNull();
        await Assert.That(clone.PreviousAddresses).IsNotSameReferenceAs(original.PreviousAddresses);
        await Assert.That(clone.PreviousAddresses!.Count).IsEqualTo(2);
        await Assert.That(clone.PreviousAddresses[0]).IsNotSameReferenceAs(original.PreviousAddresses![0]);
        await Assert.That(clone.PreviousAddresses[0]!.City).IsEqualTo("Berlin");
        await Assert.That(clone.PreviousAddresses[1]!.City).IsEqualTo("Munich");

        clone.PreviousAddresses[0]!.City = "Hamburg";
        await Assert.That(original.PreviousAddresses[0]!.City).IsEqualTo("Berlin");
    }

    [Test]
    public async Task Context_Should_Clone_Registered_Type_With_Array_Member()
    {
        Issue56ArrayContext ctx = new Issue56ArrayContext();
        Issue56ArrayHolder original = new Issue56ArrayHolder
        {
            Items = [new Issue56Address { City = "Prague" }, new Issue56Address { City = "Brno" }]
        };

        Issue56ArrayHolder clone = ctx.Clone(original)!;

        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items![0]).IsNotSameReferenceAs(original.Items![0]);
        await Assert.That(clone.Items[0]!.City).IsEqualTo("Prague");
        await Assert.That(clone.Items[1]!.City).IsEqualTo("Brno");
    }

    [Test]
    public async Task Context_Should_Clone_Registered_Type_With_Dictionary_Member()
    {
        Issue56DictionaryContext ctx = new Issue56DictionaryContext();
        Issue56DictionaryHolder original = new Issue56DictionaryHolder
        {
            Map = new Dictionary<string, Issue56Address>
            {
                ["home"] = new Issue56Address { City = "Oslo" }
            }
        };

        Issue56DictionaryHolder clone = ctx.Clone(original)!;

        await Assert.That(clone.Map).IsNotSameReferenceAs(original.Map);
        await Assert.That(clone.Map!["home"]).IsNotSameReferenceAs(original.Map!["home"]);
        await Assert.That(clone.Map["home"].City).IsEqualTo("Oslo");
    }

    [Test]
    public async Task Context_Should_Clone_Registered_Type_With_HashSet_Member()
    {
        Issue56HashSetContext ctx = new Issue56HashSetContext();
        Issue56HashSetHolder original = new Issue56HashSetHolder
        {
            Items = [new Issue56Address { City = "Lisbon" }]
        };

        Issue56HashSetHolder clone = ctx.Clone(original)!;

        await Assert.That(clone.Items).IsNotSameReferenceAs(original.Items);
        await Assert.That(clone.Items!.Count).IsEqualTo(1);
        await Assert.That(clone.Items.Any(a => a.City == "Lisbon")).IsTrue();
        await Assert.That(clone.Items.Any(a => original.Items!.Contains(a))).IsFalse();
    }

    [Test]
    public async Task Context_Should_Clone_Registered_Type_With_Nested_Dictionary_Of_List()
    {
        Issue56NestedContext ctx = new Issue56NestedContext();
        Issue56NestedHolder original = new Issue56NestedHolder
        {
            Groups = new Dictionary<string, List<Issue56Address>>
            {
                ["eu"] = [new Issue56Address { City = "Paris" }, new Issue56Address { City = "Rome" }]
            }
        };

        Issue56NestedHolder clone = ctx.Clone(original)!;

        await Assert.That(clone.Groups).IsNotSameReferenceAs(original.Groups);
        await Assert.That(clone.Groups!["eu"]).IsNotSameReferenceAs(original.Groups!["eu"]);
        await Assert.That(clone.Groups["eu"][0]).IsNotSameReferenceAs(original.Groups["eu"][0]);
        await Assert.That(clone.Groups["eu"][0].City).IsEqualTo("Paris");
        await Assert.That(clone.Groups["eu"][1].City).IsEqualTo("Rome");
    }

    /// <summary>
    /// A registered type reached through a member whose own default needs no reference tracking must
    /// still be handed the ambient state. Losing it silently drops identity preservation at that
    /// boundary even though the enclosing context does track references.
    /// </summary>
    [Test]
    public async Task Context_MemberWhoseDefaultNeedsNoState_StillReceivesTheAmbientState()
    {
        CtxPlainLeaf shared = new CtxPlainLeaf { Value = 101 };
        CtxStateRoot original = new CtxStateRoot
        {
            Left = shared,
            Right = shared,
            Cyclic = new CtxCycleNode { Value = 102 }
        };

        CtxStateContext ctx = new CtxStateContext();
        CtxStateRoot clone = ctx.Clone(original)!;

        await Assert.That(clone.Cyclic).IsNotNull();
        await Assert.That(clone.Cyclic!.Value).IsEqualTo(102);
        await Assert.That(clone.Left).IsNotSameReferenceAs(original.Left);
        await Assert.That(clone.Left!.Value).IsEqualTo(101);
        await Assert.That(clone.Left).IsSameReferenceAs(clone.Right).Because(
            "the context tracks references, so a registered-type member must share its state");
    }
}

/// <summary>Plain registered type with no reference members of its own.</summary>
public class CtxPlainLeaf
{
    public int Value { get; set; }
}

/// <summary>Registered type that makes the context graph cycle-capable.</summary>
public class CtxCycleNode
{
    public int Value { get; set; }

    public CtxCycleNode? Next { get; set; }
}

public class CtxStateRoot
{
    public CtxPlainLeaf? Left { get; set; }

    public CtxPlainLeaf? Right { get; set; }

    public CtxCycleNode? Cyclic { get; set; }
}

[FastClonerRegister(typeof(CtxStateRoot), typeof(CtxPlainLeaf), typeof(CtxCycleNode))]
public partial class CtxStateContext : FastClonerContext { }

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

// Issue #56: identity-tracking members (collections/arrays/dictionaries of non-safe
// element types) must compile and produce an independent deep copy.
public class Issue56Customer
{
    public string Name { get; set; } = string.Empty;
    public Issue56Address? Address { get; set; }
    public List<Issue56Address>? PreviousAddresses { get; set; }
}

public class Issue56Address
{
    public string City { get; set; } = string.Empty;
}

[FastClonerRegister(typeof(Issue56Customer), typeof(Issue56Address))]
public partial class Issue56ListContext : FastClonerContext {}

public class Issue56ArrayHolder
{
    public Issue56Address[]? Items { get; set; }
}

[FastClonerRegister(typeof(Issue56ArrayHolder), typeof(Issue56Address))]
public partial class Issue56ArrayContext : FastClonerContext {}

public class Issue56DictionaryHolder
{
    public Dictionary<string, Issue56Address>? Map { get; set; }
}

[FastClonerRegister(typeof(Issue56DictionaryHolder), typeof(Issue56Address))]
public partial class Issue56DictionaryContext : FastClonerContext {}

public class Issue56HashSetHolder
{
    public HashSet<Issue56Address>? Items { get; set; }
}

[FastClonerRegister(typeof(Issue56HashSetHolder), typeof(Issue56Address))]
public partial class Issue56HashSetContext : FastClonerContext {}

public class Issue56NestedHolder
{
    public Dictionary<string, List<Issue56Address>>? Groups { get; set; }
}

[FastClonerRegister(typeof(Issue56NestedHolder), typeof(Issue56Address))]
public partial class Issue56NestedContext : FastClonerContext {}