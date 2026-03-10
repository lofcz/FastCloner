using FastCloner.Code;
using System.Threading.Tasks;

namespace FastCloner.Tests;
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
    public async Task FastClonerReference_PreservesReference()
    {
        // Arrange
        SharedService service = new SharedService { ServiceName = "TestService", InstanceId = 42 };
        ClassWithReferenceAttribute original = new ClassWithReferenceAttribute
        {
            Name = "Test",
            SharedService = service,
            Data = new InnerData { Value = "DataValue" }
        };

        // Act
        ClassWithReferenceAttribute clone = original.DeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsEqualTo("Test");

        // SharedService should be the SAME reference (not cloned)
        await Assert.That(clone.SharedService).IsSameReferenceAs(original.SharedService);
        await Assert.That(clone.SharedService.InstanceId).IsEqualTo(42);

        // Data should be a DIFFERENT reference (deep cloned)
        await Assert.That(clone.Data).IsNotSameReferenceAs(original.Data);
        await Assert.That(clone.Data.Value).IsEqualTo("DataValue");
    }

    [Test]
    public async Task FastClonerReference_WithNullValue_ClonesSuccessfully()
    {
        // Arrange
        ClassWithReferenceAttribute original = new ClassWithReferenceAttribute
        {
            Name = "Test",
            SharedService = null!,
            Data = new InnerData { Value = "DataValue" }
        };

        // Act
        ClassWithReferenceAttribute clone = original.DeepClone();

        // Assert
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone.SharedService).IsNull();
        await Assert.That(clone.Data).IsNotSameReferenceAs(original.Data);
    }

    #endregion

    #region [FastClonerBehavior] Explicit Tests

    [Test]
    public async Task FastClonerBehavior_Reference_PreservesReference()
    {
        // Arrange
        ClassWithExplicitBehavior original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        ClassWithExplicitBehavior clone = original.DeepClone();

        // Assert - Reference behavior
        await Assert.That(clone.Service1).IsSameReferenceAs(original.Service1);
    }

    [Test]
    public async Task FastClonerBehavior_Shallow_CopiesReference()
    {
        // Arrange
        ClassWithExplicitBehavior original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        ClassWithExplicitBehavior clone = original.DeepClone();

        // Assert - Shallow behavior copies reference directly
        await Assert.That(clone.ShallowData).IsSameReferenceAs(original.ShallowData);
    }

    [Test]
    public async Task FastClonerBehavior_Ignore_SetsToNull()
    {
        // Arrange
        ClassWithExplicitBehavior original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        ClassWithExplicitBehavior clone = original.DeepClone();

        // Assert - Ignored behavior sets to null/default
        await Assert.That(clone.IgnoredData).IsNull();
    }

    [Test]
    public async Task FastClonerBehavior_Clone_DeepClones()
    {
        // Arrange
        ClassWithExplicitBehavior original = new ClassWithExplicitBehavior
        {
            Name = "Test",
            Service1 = new SharedService { ServiceName = "Svc", InstanceId = 1 },
            ShallowData = new InnerData { Value = "Shallow" },
            IgnoredData = new InnerData { Value = "Ignored" },
            ExplicitCloneData = new InnerData { Value = "Cloned" }
        };

        // Act
        ClassWithExplicitBehavior clone = original.DeepClone();

        // Assert - Explicit Clone behavior deep clones
        await Assert.That(clone.ExplicitCloneData).IsNotSameReferenceAs(original.ExplicitCloneData);
        await Assert.That(clone.ExplicitCloneData.Value).IsEqualTo("Cloned");
    }

    #endregion

    #region Mixed Attribute Tests

    [Test]
    public async Task MixedAttributes_AllBehaviorsWorkCorrectly()
    {
        // Arrange
        MixedAttributeClass original = new MixedAttributeClass
        {
            Ignored = new InnerData { Value = "Ignored" },
            Shallow = new InnerData { Value = "Shallow" },
            Reference = new SharedService { ServiceName = "Ref", InstanceId = 1 },
            Normal = new InnerData { Value = "Normal" }
        };

        // Act
        MixedAttributeClass clone = original.DeepClone();

        // Assert
        await Assert.That(clone.Ignored).IsNull();                          // [FastClonerIgnore] -> null
        await Assert.That(clone.Shallow).IsSameReferenceAs(original.Shallow);      // [FastClonerShallow] -> same reference
        await Assert.That(clone.Reference).IsSameReferenceAs(original.Reference);  // [FastClonerReference] -> same reference
        await Assert.That(clone.Normal).IsNotSameReferenceAs(original.Normal);    // No attribute -> deep cloned
        await Assert.That(clone.Normal.Value).IsEqualTo("Normal");
    }

    #endregion

    #region [FastClonerIgnore(bool)] Tests

    [Test]
    public async Task FastClonerIgnore_WithFalse_DoesNotIgnore()
    {
        // Arrange
        ClassWithIgnoreFalse original = new ClassWithIgnoreFalse
        {
            NotIgnored = new InnerData { Value = "NotIgnored" },
            IsIgnored = new InnerData { Value = "IsIgnored" }
        };

        // Act
        ClassWithIgnoreFalse clone = original.DeepClone();

        // Assert
        // NotIgnored should be deep cloned (ignored=false means NOT ignored)
        await Assert.That(clone.NotIgnored).IsNotNull();
        await Assert.That(clone.NotIgnored).IsNotSameReferenceAs(original.NotIgnored);
        await Assert.That(clone.NotIgnored.Value).IsEqualTo("NotIgnored");

        // IsIgnored should be null (ignored=true means ignored)
        await Assert.That(clone.IsIgnored).IsNull();
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
    public async Task TypeLevelReference_PreservesReference()
    {
        // Arrange
        ContainerWithTypeBehaviors original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        ContainerWithTypeBehaviors clone = original.DeepClone();

        // Assert - TypeMarkedAsReference should preserve reference
        await Assert.That(clone.RefType).IsSameReferenceAs(original.RefType);
    }

    [Test]
    public async Task TypeLevelIgnore_SetsToNull()
    {
        // Arrange
        ContainerWithTypeBehaviors original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        ContainerWithTypeBehaviors clone = original.DeepClone();

        // Assert - TypeMarkedAsIgnored should be null
        await Assert.That(clone.IgnoredType).IsNull();
    }

    [Test]
    public async Task TypeLevelShallow_CopiesReference()
    {
        // Arrange
        ContainerWithTypeBehaviors original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        ContainerWithTypeBehaviors clone = original.DeepClone();

        // Assert - TypeMarkedAsShallow should be same reference (shallow clone for members = copy reference)
        await Assert.That(clone.ShallowType).IsSameReferenceAs(original.ShallowType);
    }

    [Test]
    public async Task TypeLevel_NormalType_DeepClones()
    {
        // Arrange
        ContainerWithTypeBehaviors original = new ContainerWithTypeBehaviors
        {
            RefType = new TypeMarkedAsReference { Name = "Ref", Value = 42 },
            IgnoredType = new TypeMarkedAsIgnored { Data = "Ignored" },
            ShallowType = new TypeMarkedAsShallow { Name = "Shallow", Inner = new InnerData { Value = "Inner" } },
            NormalType = new InnerData { Value = "Normal" }
        };

        // Act
        ContainerWithTypeBehaviors clone = original.DeepClone();

        // Assert - Normal type should be deep cloned
        await Assert.That(clone.NormalType).IsNotSameReferenceAs(original.NormalType);
        await Assert.That(clone.NormalType.Value).IsEqualTo("Normal");
    }

    [Test]
    public async Task MemberLevelOverride_TakesPrecedenceOverTypeLevel()
    {
        // Arrange
        ContainerWithMemberOverride original = new ContainerWithMemberOverride
        {
            OverriddenToClone = new TypeMarkedAsReference { Name = "Override", Value = 1 },
            DefaultBehavior = new TypeMarkedAsReference { Name = "Default", Value = 2 }
        };

        // Act
        ContainerWithMemberOverride clone = original.DeepClone();

        // Assert
        // OverriddenToClone should be deep cloned (member-level override)
        await Assert.That(clone.OverriddenToClone).IsNotSameReferenceAs(original.OverriddenToClone);
        await Assert.That(clone.OverriddenToClone.Name).IsEqualTo("Override");
        await Assert.That(clone.OverriddenToClone.Value).IsEqualTo(1);

        // DefaultBehavior should preserve reference (type-level behavior)
        await Assert.That(clone.DefaultBehavior).IsSameReferenceAs(original.DefaultBehavior);
    }

    #endregion
}