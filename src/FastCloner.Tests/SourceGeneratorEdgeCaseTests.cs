using System.Collections.ObjectModel;
using FastCloner.SourceGenerator.Shared;

namespace FastCloner.Tests;

/// <summary>
/// Tests for edge cases and issues fixed in the source generator.
/// Each test group covers a specific issue that was identified and fixed.
/// </summary>
[TestFixture]
[SourceGeneratorCompatible]
public class SourceGeneratorEdgeCaseTests
{
    #region Issue 1: Multi-dimensional Arrays

    [FastClonerClonable]
    public class ClassWith2dArray
    {
        public int[,]? Matrix { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWith3dArray
    {
        public double[,,]? Cube { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void MultiDimensionalArray_2D_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWith2dArray
        {
            Name = "Test",
            Matrix = new int[2, 3]
            {
                { 1, 2, 3 },
                { 4, 5, 6 }
            }
        };

        // Act - requires FastCloner runtime for multi-dimensional arrays
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        Assert.That(clone.Matrix, Is.Not.Null);
        Assert.That(clone.Matrix, Is.Not.SameAs(original.Matrix));
        Assert.That(clone.Matrix![0, 0], Is.EqualTo(1));
        Assert.That(clone.Matrix[1, 2], Is.EqualTo(6));

        // Verify independence
        original.Matrix![0, 0] = 999;
        Assert.That(clone.Matrix[0, 0], Is.EqualTo(1));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void MultiDimensionalArray_3D_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWith3dArray
        {
            Cube = new double[2, 2, 2]
        };
        original.Cube[0, 0, 0] = 1.1;
        original.Cube[1, 1, 1] = 2.2;

        // Act - requires FastCloner runtime for multi-dimensional arrays
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Cube, Is.Not.Null);
        Assert.That(clone.Cube, Is.Not.SameAs(original.Cube));
        Assert.That(clone.Cube![0, 0, 0], Is.EqualTo(1.1));
        Assert.That(clone.Cube[1, 1, 1], Is.EqualTo(2.2));
    }

    #endregion

    #region Issue 2: Fields in Object Initializers

    [FastClonerClonable]
    public class ClassWithFields
    {
        public int IntField;
        public string? StringField;
        public List<int>? ListField;
    }

    [FastClonerClonable]
    public class ClassWithMixedMembers
    {
        public int PropertyValue { get; set; }
        public int FieldValue;
        public string? PropertyString { get; set; }
        public string? FieldString;
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Fields_Should_Be_Cloned_In_Object_Initializers()
    {
        // Arrange
        var original = new ClassWithFields
        {
            IntField = 42,
            StringField = "Test",
            ListField = new List<int> { 1, 2, 3 }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.IntField, Is.EqualTo(42));
        Assert.That(clone.StringField, Is.EqualTo("Test"));
        Assert.That(clone.ListField, Is.Not.Null);
        Assert.That(clone.ListField, Is.Not.SameAs(original.ListField));
        Assert.That(clone.ListField, Is.EquivalentTo(new[] { 1, 2, 3 }));

        // Verify independence
        original.IntField = 999;
        original.ListField!.Add(99);
        Assert.That(clone.IntField, Is.EqualTo(42));
        Assert.That(clone.ListField!.Count, Is.EqualTo(3));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Mixed_Properties_And_Fields_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWithMixedMembers
        {
            PropertyValue = 10,
            FieldValue = 20,
            PropertyString = "PropStr",
            FieldString = "FieldStr"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.PropertyValue, Is.EqualTo(10));
        Assert.That(clone.FieldValue, Is.EqualTo(20));
        Assert.That(clone.PropertyString, Is.EqualTo("PropStr"));
        Assert.That(clone.FieldString, Is.EqualTo("FieldStr"));
    }

    #endregion

    #region Issue 3: Init-Only Properties

    [FastClonerClonable]
    public class ClassWithInitOnlyProps
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? MutableValue { get; set; }
    }

    [FastClonerClonable]
    public record RecordWithInitProps
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public List<string>? Tags { get; init; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void InitOnly_Properties_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWithInitOnlyProps
        {
            Id = 123,
            Name = "Test",
            MutableValue = "Mutable"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Id, Is.EqualTo(123));
        Assert.That(clone.Name, Is.EqualTo("Test"));
        Assert.That(clone.MutableValue, Is.EqualTo("Mutable"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Record_With_Init_Properties_Should_Be_Cloned()
    {
        // Arrange
        var original = new RecordWithInitProps
        {
            Id = 456,
            Name = "RecordTest",
            Tags = new List<string> { "tag1", "tag2" }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Id, Is.EqualTo(456));
        Assert.That(clone.Name, Is.EqualTo("RecordTest"));
        Assert.That(clone.Tags, Is.Not.Null);
        Assert.That(clone.Tags, Is.Not.SameAs(original.Tags));
        Assert.That(clone.Tags, Is.EquivalentTo(new[] { "tag1", "tag2" }));
    }

    #endregion

    #region Issue 4: Private Setter Accessibility
    
    // Note: Properties with private setters should be SKIPPED by the source generator
    // because extension classes can't access them. We verify this compiles and works
    // for the accessible properties.

    [FastClonerClonable]
    public class ClassWithPrivateSetter
    {
        public int PublicProperty { get; set; }
        public int PrivateSetterProperty { get; private set; }
        
        // Method to set the private property for testing
        public void SetPrivate(int value) => PrivateSetterProperty = value;
    }

    [Test]
    [SourceGeneratorCompatible]
    public void PrivateSetter_Properties_Should_Be_Skipped()
    {
        // Arrange
        var original = new ClassWithPrivateSetter
        {
            PublicProperty = 100
        };
        original.SetPrivate(200);

        // Act
        var clone = original.FastDeepClone();

        // Assert - public property should be cloned, private setter property will be default
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.PublicProperty, Is.EqualTo(100));
        // Private setter property is not cloned (can't access from extension class)
        // It will have default value
        Assert.That(clone.PrivateSetterProperty, Is.EqualTo(0));
    }

    #endregion

    #region Issue 5: Delegate and Behavioral Types (Lazy, Func, Task)

    [FastClonerClonable]
    public class ClassWithDelegates
    {
        public Func<int>? IntFunc { get; set; }
        public Action? SimpleAction { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithLazy
    {
        public Lazy<string>? LazyValue { get; set; }
        public int RegularValue { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithWeakReference
    {
        public WeakReference<object>? WeakRef { get; set; }
        public string? Name { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Delegates_Should_Be_Shallow_Copied()
    {
        // Arrange
        int counter = 0;
        var original = new ClassWithDelegates
        {
            IntFunc = () => ++counter,
            SimpleAction = () => counter++,
            Name = "Test"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        // Delegates should be the same reference (shallow copied)
        Assert.That(clone.IntFunc, Is.SameAs(original.IntFunc));
        Assert.That(clone.SimpleAction, Is.SameAs(original.SimpleAction));
        
        // Both should reference the same counter
        clone.IntFunc!();
        Assert.That(counter, Is.EqualTo(1));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Lazy_Should_Be_Shallow_Copied()
    {
        // Arrange
        int initCount = 0;
        var original = new ClassWithLazy
        {
            LazyValue = new Lazy<string>(() =>
            {
                initCount++;
                return "Initialized";
            }),
            RegularValue = 42
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.RegularValue, Is.EqualTo(42));
        // Lazy should be same reference (shallow copied)
        Assert.That(clone.LazyValue, Is.SameAs(original.LazyValue));
        
        // Accessing value should only initialize once
        var _ = clone.LazyValue!.Value;
        var __ = original.LazyValue!.Value;
        Assert.That(initCount, Is.EqualTo(1)); // Should be 1 because same Lazy instance
    }

    [Test]
    [SourceGeneratorCompatible]
    public void WeakReference_Should_Be_Shallow_Copied()
    {
        // Arrange
        var target = new object();
        var original = new ClassWithWeakReference
        {
            WeakRef = new WeakReference<object>(target),
            Name = "Test"
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        // WeakReference should be same instance (shallow copied)
        Assert.That(clone.WeakRef, Is.SameAs(original.WeakRef));
        
        // Both should reference the same target
        original.WeakRef!.TryGetTarget(out var origTarget);
        clone.WeakRef!.TryGetTarget(out var cloneTarget);
        Assert.That(cloneTarget, Is.SameAs(origTarget));
    }

    #endregion
    
    // Test combining multiple edge cases

    [FastClonerClonable]
    public class ComplexEdgeCase
    {
        public int[,]? Matrix { get; set; }
        public List<int>? ListField;
        public string? Name { get; init; }
        public Func<bool>? Predicate { get; set; }
        public int RegularProp { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Complex_EdgeCase_Combining_Multiple_Issues()
    {
        // Arrange
        var original = new ComplexEdgeCase
        {
            Matrix = new int[2, 2] { { 1, 2 }, { 3, 4 } },
            ListField = new List<int> { 10, 20, 30 },
            Name = "Complex",
            Predicate = () => true,
            RegularProp = 999
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        
        // Multi-dim array should be cloned
        Assert.That(clone!.Matrix, Is.Not.Null);
        Assert.That(clone.Matrix, Is.Not.SameAs(original.Matrix));
        
        // Field should be cloned
        Assert.That(clone.ListField, Is.Not.Null);
        Assert.That(clone.ListField, Is.Not.SameAs(original.ListField));
        Assert.That(clone.ListField!.Count, Is.EqualTo(3));
        
        // Init property should be cloned
        Assert.That(clone.Name, Is.EqualTo("Complex"));
        
        // Delegate should be shallow copied
        Assert.That(clone.Predicate, Is.SameAs(original.Predicate));
        
        // Regular property should be cloned
        Assert.That(clone.RegularProp, Is.EqualTo(999));

        // Verify independence
        original.ListField!.Add(100);
        original.Matrix![0, 0] = 999;
        Assert.That(clone.ListField.Count, Is.EqualTo(3));
        Assert.That(clone.Matrix![0, 0], Is.EqualTo(1));
    }

    #region Issue 6b: Multi-dimensional Arrays with Special Types

    [FastClonerClonable]
    public class ClassWithHttpClientMatrix
    {
        public HttpClient[,]? ClientMatrix { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithHttpClient3dArray
    {
        public HttpClient[,,]? ClientCube { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void MultiDimArray_HttpClient_Should_Be_DeepCloned()
    {
        // Arrange
        var client1 = new HttpClient();
        var client2 = new HttpClient();
        var client3 = new HttpClient();
        var client4 = new HttpClient();

        var original = new ClassWithHttpClientMatrix
        {
            Name = "HttpClientTest",
            ClientMatrix = new HttpClient[2, 2]
            {
                { client1, client2 },
                { client3, client4 }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("HttpClientTest"));
        Assert.That(clone.ClientMatrix, Is.Not.Null);
        
        // The matrix itself should be a different reference (new array)
        Assert.That(clone.ClientMatrix, Is.Not.SameAs(original.ClientMatrix));
        
        // HttpClient instances should be deep cloned (different references)
        Assert.That(clone.ClientMatrix![0, 0], Is.Not.SameAs(client1));
        Assert.That(clone.ClientMatrix[0, 1], Is.Not.SameAs(client2));
        Assert.That(clone.ClientMatrix[1, 0], Is.Not.SameAs(client3));
        Assert.That(clone.ClientMatrix[1, 1], Is.Not.SameAs(client4));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void MultiDimArray_3D_HttpClient_Should_Be_DeepCloned()
    {
        // Arrange
        var clients = new HttpClient[2, 2, 2];
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                for (int k = 0; k < 2; k++)
                    clients[i, j, k] = new HttpClient();

        var original = new ClassWithHttpClient3dArray
        {
            ClientCube = clients
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.ClientCube, Is.Not.Null);
        Assert.That(clone.ClientCube, Is.Not.SameAs(original.ClientCube));
        
        // All HttpClient instances should be deep cloned (different references)
        for (int i = 0; i < 2; i++)
            for (int j = 0; j < 2; j++)
                for (int k = 0; k < 2; k++)
                    Assert.That(clone.ClientCube![i, j, k], Is.Not.SameAs(original.ClientCube![i, j, k]));
    }

    #endregion

    #region Issue 7: Jagged Arrays

    [FastClonerClonable]
    public class ClassWithJaggedArray
    {
        public int[][]? JaggedInts { get; set; }
        public string? Name { get; set; }
    }

    [FastClonerClonable]
    public class ClassWith3LevelJaggedArray
    {
        public int[][][]? DeepJagged { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithJaggedArrayOfObjects
    {
        public SimpleItem[][]? Items { get; set; }
    }

    public class SimpleItem
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void JaggedArray_2D_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWithJaggedArray
        {
            Name = "Test",
            JaggedInts = new int[][]
            {
                new int[] { 1, 2, 3 },
                new int[] { 4, 5 },
                new int[] { 6, 7, 8, 9 }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        Assert.That(clone.JaggedInts, Is.Not.Null);
        Assert.That(clone.JaggedInts, Is.Not.SameAs(original.JaggedInts));
        Assert.That(clone.JaggedInts!.Length, Is.EqualTo(3));
        
        // Each inner array should be a different reference
        Assert.That(clone.JaggedInts[0], Is.Not.SameAs(original.JaggedInts![0]));
        Assert.That(clone.JaggedInts[1], Is.Not.SameAs(original.JaggedInts[1]));
        Assert.That(clone.JaggedInts[2], Is.Not.SameAs(original.JaggedInts[2]));
        
        // Values should be the same
        Assert.That(clone.JaggedInts[0], Is.EquivalentTo(new[] { 1, 2, 3 }));
        Assert.That(clone.JaggedInts[1], Is.EquivalentTo(new[] { 4, 5 }));
        Assert.That(clone.JaggedInts[2], Is.EquivalentTo(new[] { 6, 7, 8, 9 }));

        // Verify independence
        original.JaggedInts[0][0] = 999;
        Assert.That(clone.JaggedInts[0][0], Is.EqualTo(1));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void JaggedArray_3D_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWith3LevelJaggedArray
        {
            DeepJagged = new int[][][]
            {
                new int[][]
                {
                    new int[] { 1, 2 },
                    new int[] { 3, 4, 5 }
                },
                new int[][]
                {
                    new int[] { 6 }
                }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.DeepJagged, Is.Not.Null);
        Assert.That(clone.DeepJagged, Is.Not.SameAs(original.DeepJagged));
        Assert.That(clone.DeepJagged!.Length, Is.EqualTo(2));
        
        // All levels should be different references
        Assert.That(clone.DeepJagged[0], Is.Not.SameAs(original.DeepJagged![0]));
        Assert.That(clone.DeepJagged[0][0], Is.Not.SameAs(original.DeepJagged[0][0]));
        Assert.That(clone.DeepJagged[1][0], Is.Not.SameAs(original.DeepJagged[1][0]));
        
        // Values should be preserved
        Assert.That(clone.DeepJagged[0][0], Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(clone.DeepJagged[0][1], Is.EquivalentTo(new[] { 3, 4, 5 }));
        Assert.That(clone.DeepJagged[1][0], Is.EquivalentTo(new[] { 6 }));

        // Verify independence
        original.DeepJagged[0][0][0] = 999;
        Assert.That(clone.DeepJagged[0][0][0], Is.EqualTo(1));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void JaggedArray_WithObjects_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWithJaggedArrayOfObjects
        {
            Items = new SimpleItem[][]
            {
                new SimpleItem[]
                {
                    new SimpleItem { Name = "A1", Value = 1 },
                    new SimpleItem { Name = "A2", Value = 2 }
                },
                new SimpleItem[]
                {
                    new SimpleItem { Name = "B1", Value = 10 }
                }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.Length, Is.EqualTo(2));
        
        // Inner arrays should be different references
        Assert.That(clone.Items[0], Is.Not.SameAs(original.Items![0]));
        Assert.That(clone.Items[1], Is.Not.SameAs(original.Items[1]));
        
        // Objects should be different references but same values
        Assert.That(clone.Items[0][0], Is.Not.SameAs(original.Items[0][0]));
        Assert.That(clone.Items[0][0].Name, Is.EqualTo("A1"));
        Assert.That(clone.Items[0][0].Value, Is.EqualTo(1));
        
        Assert.That(clone.Items[1][0], Is.Not.SameAs(original.Items[1][0]));
        Assert.That(clone.Items[1][0].Name, Is.EqualTo("B1"));
        Assert.That(clone.Items[1][0].Value, Is.EqualTo(10));

        // Verify independence
        original.Items[0][0].Name = "Changed";
        Assert.That(clone.Items[0][0].Name, Is.EqualTo("A1"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void JaggedArray_WithNullElements_Should_Be_Handled()
    {
        // Arrange
        var original = new ClassWithJaggedArray
        {
            Name = "WithNulls",
            JaggedInts = new int[][]
            {
                new int[] { 1, 2 },
                null!,  // null element in the outer array
                new int[] { 3 }
            }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.JaggedInts, Is.Not.Null);
        Assert.That(clone.JaggedInts!.Length, Is.EqualTo(3));
        Assert.That(clone.JaggedInts[0], Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(clone.JaggedInts[1], Is.Null);
        Assert.That(clone.JaggedInts[2], Is.EquivalentTo(new[] { 3 }));
    }

    #endregion

    #region Additional Tests for Struct Fields

    [FastClonerClonable]
    public struct StructWithFields
    {
        public int IntField;
        public string? StringProp { get; set; }
        public List<int>? ListField;
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Struct_With_Fields_Should_Be_Cloned()
    {
        // Arrange
        var original = new StructWithFields
        {
            IntField = 42,
            StringProp = "Test",
            ListField = new List<int> { 1, 2, 3 }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone.IntField, Is.EqualTo(42));
        Assert.That(clone.StringProp, Is.EqualTo("Test"));
        Assert.That(clone.ListField, Is.Not.Null);
        // Note: For value types, the list is a new reference due to the struct copy
        Assert.That(clone.ListField, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    #endregion

    #region Issue 8: Required Members (Runtime Fallback)

    [FastClonerClonable]
    public class ClassWithRequiredMembers
    {
        public required string RequiredName { get; set; }
        public required int RequiredId { get; set; }
        public string? OptionalDescription { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Class_With_Required_Members_Should_Be_Cloned()
    {
        // Arrange
        var original = new ClassWithRequiredMembers 
        { 
            RequiredName = "Required", 
            RequiredId = 123,
            OptionalDescription = "Optional" 
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone!.RequiredName, Is.EqualTo("Required"));
        Assert.That(clone.RequiredId, Is.EqualTo(123));
        Assert.That(clone.OptionalDescription, Is.EqualTo("Optional"));
    }

    #endregion

    #region Issue 9: Init-Only Properties with Circular References (Runtime Fallback)

    [FastClonerClonable]
    public class ClassWithInitAndCycle
    {
        public string? Name { get; init; }
        public ClassWithInitAndCycle? Self { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Class_With_Init_Properties_And_Cycles_Should_Be_Deep_Cloned()
    {
        // Arrange
        var original = new ClassWithInitAndCycle
        {
            Name = "CyclicInit"
        };
        original.Self = original;

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(original));
        Assert.That(clone!.Name, Is.EqualTo("CyclicInit")); // This would be null without the fix
        Assert.That(clone.Self, Is.SameAs(clone));
    }

    #endregion

    #region Issue 10: Structs with Readonly Reference Fields (Runtime Fallback)

    [FastClonerClonable]
    public struct StructWithReadonlyRefs
    {
        public readonly List<int> ReadonlyList;
        public int NormalField;

        public StructWithReadonlyRefs(List<int> list, int val)
        {
            ReadonlyList = list;
            NormalField = val;
        }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Struct_With_Readonly_Reference_Fields_Should_Be_Deep_Cloned()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };
        var original = new StructWithReadonlyRefs(list, 42);

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone.NormalField, Is.EqualTo(42));
        Assert.That(clone.ReadonlyList, Is.Not.Null);
        Assert.That(clone.ReadonlyList, Is.Not.SameAs(original.ReadonlyList)); // This would fail (be SameAs) without the fix
        Assert.That(clone.ReadonlyList, Is.EquivalentTo(new[] { 1, 2, 3 }));
        
        // Verify independence
        list.Add(4);
        Assert.That(clone.ReadonlyList.Count, Is.EqualTo(3));
    }

    #endregion

    #region Issue 11: ObservableCollection Properties Without Setters (GitHub Issue #19)

    // Test class for ObservableCollection with getter only - currently NOT supported
    // This represents the bug reported in GitHub Issue #19
    [FastClonerClonable]
    public class ClassWithObservableCollectionGetterOnly
    {
        public ObservableCollection<string> Items { get; } = new();
        public string? Name { get; set; }
    }

    // Test class for ObservableCollection with getter and setter - should work
    [FastClonerClonable]
    public class ClassWithObservableCollectionGetterSetter
    {
        public ObservableCollection<string>? Items { get; set; }
        public string? Name { get; set; }
    }

    // Test class for ObservableCollection with init - workaround from the issue
    [FastClonerClonable]
    public class ClassWithObservableCollectionInit
    {
        public ObservableCollection<string> Items { get; init; } = new();
        public string? Name { get; set; }
    }

    // Test class with nested object in ObservableCollection
    public class ObservableItem
    {
        public string? Value { get; set; }
        public int Id { get; set; }
    }

    [FastClonerClonable]
    public class ClassWithObservableCollectionOfObjects
    {
        public ObservableCollection<ObservableItem> Items { get; } = new();
        public string? Description { get; set; }
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ObservableCollection_WithGetterOnly_Should_Be_Cloned()
    {
        // Arrange - This test documents the expected behavior for Issue #19
        // Currently this scenario is NOT supported by the source generator
        var original = new ClassWithObservableCollectionGetterOnly
        {
            Name = "Test"
        };
        original.Items.Add("Item1");
        original.Items.Add("Item2");
        original.Items.Add("Item3");

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        Assert.That(clone.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.EqualTo("Item1"));
        Assert.That(clone.Items[1], Is.EqualTo("Item2"));
        Assert.That(clone.Items[2], Is.EqualTo("Item3"));

        // Verify independence
        original.Items.Add("NewItem");
        original.Items[0] = "Modified";
        Assert.That(clone.Items.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.EqualTo("Item1"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ObservableCollection_WithGetterSetter_Should_Be_Cloned()
    {
        // Arrange - This scenario is already supported
        var original = new ClassWithObservableCollectionGetterSetter
        {
            Name = "Test",
            Items = new ObservableCollection<string> { "Item1", "Item2", "Item3" }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        Assert.That(clone.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items!.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.EqualTo("Item1"));
        Assert.That(clone.Items[1], Is.EqualTo("Item2"));
        Assert.That(clone.Items[2], Is.EqualTo("Item3"));

        // Verify independence
        original.Items!.Add("NewItem");
        original.Items[0] = "Modified";
        Assert.That(clone.Items.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.EqualTo("Item1"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ObservableCollection_WithInit_Should_Be_Cloned()
    {
        // Arrange - This is the workaround mentioned in Issue #19
        var original = new ClassWithObservableCollectionInit
        {
            Name = "Test",
            Items = new ObservableCollection<string> { "Item1", "Item2", "Item3" }
        };

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Name, Is.EqualTo("Test"));
        Assert.That(clone.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.EqualTo("Item1"));
        Assert.That(clone.Items[1], Is.EqualTo("Item2"));
        Assert.That(clone.Items[2], Is.EqualTo("Item3"));

        // Verify independence
        original.Items.Add("NewItem");
        original.Items[0] = "Modified";
        Assert.That(clone.Items.Count, Is.EqualTo(3));
        Assert.That(clone.Items[0], Is.EqualTo("Item1"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void ObservableCollection_WithObjects_GetterOnly_Should_Be_Deep_Cloned()
    {
        // Arrange - Test with nested objects to verify deep cloning
        var original = new ClassWithObservableCollectionOfObjects
        {
            Description = "Container"
        };
        original.Items.Add(new ObservableItem { Value = "First", Id = 1 });
        original.Items.Add(new ObservableItem { Value = "Second", Id = 2 });

        // Act
        var clone = original.FastDeepClone();

        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone!.Description, Is.EqualTo("Container"));
        Assert.That(clone.Items, Is.Not.Null);
        Assert.That(clone.Items, Is.Not.SameAs(original.Items));
        Assert.That(clone.Items.Count, Is.EqualTo(2));
        
        // Verify objects are also deep cloned (different references)
        Assert.That(clone.Items[0], Is.Not.SameAs(original.Items[0]));
        Assert.That(clone.Items[1], Is.Not.SameAs(original.Items[1]));
        Assert.That(clone.Items[0].Value, Is.EqualTo("First"));
        Assert.That(clone.Items[0].Id, Is.EqualTo(1));
        Assert.That(clone.Items[1].Value, Is.EqualTo("Second"));
        Assert.That(clone.Items[1].Id, Is.EqualTo(2));

        // Verify independence
        original.Items[0].Value = "Modified";
        original.Items.Add(new ObservableItem { Value = "Third", Id = 3 });
        Assert.That(clone.Items[0].Value, Is.EqualTo("First"));
        Assert.That(clone.Items.Count, Is.EqualTo(2));
    }

    #endregion
}
