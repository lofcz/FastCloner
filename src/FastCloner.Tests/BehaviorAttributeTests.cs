using FastCloner.Code;
using NUnit.Framework;

namespace FastCloner.Tests;

/// <summary>
/// Tests for the new consolidated attribute system including [FastClonerReference] and [FastClonerBehavior].
/// </summary>
[TestFixture]
public class BehaviorAttributeTests
{
    #region Test Classes

    /// <summary>
    /// Test class with [FastClonerReference] attribute.
    /// </summary>
    public class ClassWithReferenceAttribute
    {
        public string Name { get; set; } = "";
        
        [FastClonerReference]
        public SharedService SharedService { get; set; } = null!;
        
        public InnerData Data { get; set; } = null!;
    }

    /// <summary>
    /// Simulates a shared/singleton service that should not be cloned.
    /// </summary>
    public class SharedService
    {
        public string ServiceName { get; set; } = "";
        public int InstanceId { get; set; }
    }

    /// <summary>
    /// Regular data class that should be deep cloned.
    /// </summary>
    public class InnerData
    {
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// Test class with [FastClonerBehavior] attribute using different behaviors.
    /// </summary>
    public class ClassWithExplicitBehavior
    {
        public string Name { get; set; } = "";
        
        [FastClonerBehavior(CloneBehavior.Reference)]
        public SharedService Service1 { get; set; } = null!;
        
        [FastClonerBehavior(CloneBehavior.Shallow)]
        public InnerData ShallowData { get; set; } = null!;
        
        [FastClonerBehavior(CloneBehavior.Ignore)]
        public InnerData IgnoredData { get; set; } = null!;
        
        [FastClonerBehavior(CloneBehavior.Clone)]
        public InnerData ExplicitCloneData { get; set; } = null!;
    }

    /// <summary>
    /// Test class mixing shorthand and explicit attributes.
    /// </summary>
    public class MixedAttributeClass
    {
        [FastClonerIgnore]
        public InnerData Ignored { get; set; } = null!;
        
        [FastClonerShallow]
        public InnerData Shallow { get; set; } = null!;
        
        [FastClonerReference]
        public SharedService Reference { get; set; } = null!;
        
        public InnerData Normal { get; set; } = null!;
    }

    /// <summary>
    /// Test class with [FastClonerIgnore(false)] to opt out of ignoring.
    /// </summary>
    public class ClassWithIgnoreFalse
    {
        [FastClonerIgnore(false)]  // Should NOT be ignored
        public InnerData NotIgnored { get; set; } = null!;
        
        [FastClonerIgnore(true)]   // Should be ignored
        public InnerData IsIgnored { get; set; } = null!;
    }

    #endregion

    #region [FastClonerReference] Tests

    [Test]
    public void FastClonerReference_PreservesReference()
    {
        // Arrange
        var service = new SharedService { ServiceName = "TestService", InstanceId = 42 };
        var original = new ClassWithReferenceAttribute
        {
            Name = "Test",
            SharedService = service,
            Data = new InnerData { Value = "DataValue" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone.Name, Is.EqualTo("Test"));
        
        // SharedService should be the SAME reference (not cloned)
        Assert.That(clone.SharedService, Is.SameAs(original.SharedService));
        Assert.That(clone.SharedService.InstanceId, Is.EqualTo(42));
        
        // Data should be a DIFFERENT reference (deep cloned)
        Assert.That(clone.Data, Is.Not.SameAs(original.Data));
        Assert.That(clone.Data.Value, Is.EqualTo("DataValue"));
    }

    [Test]
    public void FastClonerReference_WithNullValue_ClonesSuccessfully()
    {
        // Arrange
        var original = new ClassWithReferenceAttribute
        {
            Name = "Test",
            SharedService = null!,
            Data = new InnerData { Value = "DataValue" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone.SharedService, Is.Null);
        Assert.That(clone.Data, Is.Not.SameAs(original.Data));
    }

    #endregion

    #region [FastClonerBehavior] Explicit Tests

    [Test]
    public void FastClonerBehavior_Reference_PreservesReference()
    {
        // Arrange
        var original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - Reference behavior
        Assert.That(clone.Service1, Is.SameAs(original.Service1));
    }

    [Test]
    public void FastClonerBehavior_Shallow_CopiesReference()
    {
        // Arrange
        var original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - Shallow behavior copies reference directly
        Assert.That(clone.ShallowData, Is.SameAs(original.ShallowData));
    }

    [Test]
    public void FastClonerBehavior_Ignore_SetsToNull()
    {
        // Arrange
        var original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - Ignored behavior sets to null/default
        Assert.That(clone.IgnoredData, Is.Null);
    }

    [Test]
    public void FastClonerBehavior_Clone_DeepClones()
    {
        // Arrange
        var original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - Explicit Clone behavior deep clones
        Assert.That(clone.ExplicitCloneData, Is.Not.SameAs(original.ExplicitCloneData));
        Assert.That(clone.ExplicitCloneData.Value, Is.EqualTo("Cloned"));
    }

    #endregion

    #region Mixed Attribute Tests

    [Test]
    public void MixedAttributes_AllBehaviorsWorkCorrectly()
    {
        // Arrange
        var original = new MixedAttributeClass
        {
            Ignored = new InnerData { Value = "Ignored" },
            Shallow = new InnerData { Value = "Shallow" },
            Reference = new SharedService { ServiceName = "Ref", InstanceId = 1 },
            Normal = new InnerData { Value = "Normal" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.That(clone.Ignored, Is.Null);                          // [FastClonerIgnore] -> null
        Assert.That(clone.Shallow, Is.SameAs(original.Shallow));      // [FastClonerShallow] -> same reference
        Assert.That(clone.Reference, Is.SameAs(original.Reference));  // [FastClonerReference] -> same reference
        Assert.That(clone.Normal, Is.Not.SameAs(original.Normal));    // No attribute -> deep cloned
        Assert.That(clone.Normal.Value, Is.EqualTo("Normal"));
    }

    #endregion

    #region [FastClonerIgnore(bool)] Tests

    [Test]
    public void FastClonerIgnore_WithFalse_DoesNotIgnore()
    {
        // Arrange
        var original = new ClassWithIgnoreFalse
        {
            NotIgnored = new InnerData { Value = "NotIgnored" },
            IsIgnored = new InnerData { Value = "IsIgnored" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        // NotIgnored should be deep cloned (ignored=false means NOT ignored)
        Assert.That(clone.NotIgnored, Is.Not.Null);
        Assert.That(clone.NotIgnored, Is.Not.SameAs(original.NotIgnored));
        Assert.That(clone.NotIgnored.Value, Is.EqualTo("NotIgnored"));
        
        // IsIgnored should be null (ignored=true means ignored)
        Assert.That(clone.IsIgnored, Is.Null);
    }

    #endregion

    #region Type-Level Behavior Tests

    /// <summary>
    /// A type marked with [FastClonerReference] at the type level.
    /// All usages of this type should preserve reference.
    /// </summary>
    [FastClonerReference]
    public class TypeMarkedAsReference
    {
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    /// <summary>
    /// A type marked with [FastClonerIgnore] at the type level.
    /// All usages of this type should be set to null/default.
    /// </summary>
    [FastClonerIgnore]
    public class TypeMarkedAsIgnored
    {
        public string Data { get; set; } = "";
    }

    /// <summary>
    /// A type marked with [FastClonerShallow] at the type level.
    /// All usages of this type should be shallow cloned.
    /// </summary>
    [FastClonerShallow]
    public class TypeMarkedAsShallow
    {
        public string Name { get; set; } = "";
        public InnerData Inner { get; set; } = null!;
    }

    /// <summary>
    /// Container class that uses types with type-level behavior attributes.
    /// </summary>
    public class ContainerWithTypeBehaviors
    {
        public TypeMarkedAsReference RefType { get; set; } = null!;
        public TypeMarkedAsIgnored IgnoredType { get; set; } = null!;
        public TypeMarkedAsShallow ShallowType { get; set; } = null!;
        public InnerData NormalType { get; set; } = null!;
    }

    /// <summary>
    /// Container with member-level override of type-level behavior.
    /// </summary>
    public class ContainerWithMemberOverride
    {
        // This should be deep cloned despite TypeMarkedAsReference having [FastClonerReference]
        [FastClonerBehavior(CloneBehavior.Clone)]
        public TypeMarkedAsReference OverriddenToClone { get; set; } = null!;
        
        // This should preserve reference (default type-level behavior)
        public TypeMarkedAsReference DefaultBehavior { get; set; } = null!;
    }

    [Test]
    public void TypeLevelReference_PreservesReference()
    {
        // Arrange
        var original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - TypeMarkedAsReference should preserve reference
        Assert.That(clone.RefType, Is.SameAs(original.RefType));
    }

    [Test]
    public void TypeLevelIgnore_SetsToNull()
    {
        // Arrange
        var original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - TypeMarkedAsIgnored should be null
        Assert.That(clone.IgnoredType, Is.Null);
    }

    [Test]
    public void TypeLevelShallow_CopiesReference()
    {
        // Arrange
        var original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - TypeMarkedAsShallow should be same reference (shallow clone for members = copy reference)
        Assert.That(clone.ShallowType, Is.SameAs(original.ShallowType));
    }

    [Test]
    public void TypeLevel_NormalType_DeepClones()
    {
        // Arrange
        var original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        var clone = original.DeepClone();

        // Assert - Normal type should be deep cloned
        Assert.That(clone.NormalType, Is.Not.SameAs(original.NormalType));
        Assert.That(clone.NormalType.Value, Is.EqualTo("Normal"));
    }

    [Test]
    public void MemberLevelOverride_TakesPrecedenceOverTypeLevel()
    {
        // Arrange
        var original = new ContainerWithMemberOverride
        {
            OverriddenToClone = new TypeMarkedAsReference { Name = "Override", Value = 1 },
            DefaultBehavior = new TypeMarkedAsReference { Name = "Default", Value = 2 }
        };

        // Act
        var clone = original.DeepClone();

        // Assert
        // OverriddenToClone should be deep cloned (member-level override)
        Assert.That(clone.OverriddenToClone, Is.Not.SameAs(original.OverriddenToClone));
        Assert.That(clone.OverriddenToClone.Name, Is.EqualTo("Override"));
        Assert.That(clone.OverriddenToClone.Value, Is.EqualTo(1));
        
        // DefaultBehavior should preserve reference (type-level behavior)
        Assert.That(clone.DefaultBehavior, Is.SameAs(original.DefaultBehavior));
    }

    #endregion
}
