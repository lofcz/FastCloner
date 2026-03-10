using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using FastCloner.Code;
using FastCloner.Contrib;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class SpecialCaseTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Before(Class)]
    public static void Setup()
    {
        ContribTypeHandlers.Register();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public class MyClass
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public short[] shortsArray = new short[4];

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 4)]
        public InternalClass[] internals = new InternalClass[4];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public class InternalClass
    {
        public byte myByte;
        public uint myUint1;
        public uint myUint2;
        public uint myUint3;
    }

    [Test]
    public async Task Test_DeepClone_Marshal()
    {
        // Arrange
        MyClass original = new MyClass
        {
            shortsArray = [1, 2, 3, 4],
            internals =
            [
                new InternalClass { myByte = 1, myUint1 = 10, myUint2 = 20, myUint3 = 30 },
                new InternalClass { myByte = 2, myUint1 = 11, myUint2 = 21, myUint3 = 31 },
                new InternalClass { myByte = 3, myUint1 = 12, myUint2 = 22, myUint3 = 32 },
                new InternalClass { myByte = 4, myUint1 = 13, myUint2 = 23, myUint3 = 33 }
            ]
        };

        // Act
        MyClass cloned = original.DeepClone();

        // Assert
        await Assert.That(cloned).IsNotSameReferenceAs(original);

        await Assert.That(cloned.shortsArray).IsNotSameReferenceAs(original.shortsArray);
        await Assert.That(cloned.shortsArray).IsEquivalentTo(original.shortsArray);

        await Assert.That(cloned.internals).IsNotSameReferenceAs(original.internals);
        await Assert.That(cloned.internals.Length).IsEqualTo(original.internals.Length);

        for (int i = 0; i < original.internals.Length; i++)
        {
            await Assert.That(cloned.internals[i]).IsNotSameReferenceAs(original.internals[i]);
            await Assert.That(cloned.internals[i].myByte).IsEqualTo(original.internals[i].myByte);
            await Assert.That(cloned.internals[i].myUint1).IsEqualTo(original.internals[i].myUint1);
            await Assert.That(cloned.internals[i].myUint2).IsEqualTo(original.internals[i].myUint2);
            await Assert.That(cloned.internals[i].myUint3).IsEqualTo(original.internals[i].myUint3);
        }
    }

    [Test]
    public async Task Test_InitOnlyProperties_ObjectInitialization()
    {
        // Arrange & Act
        PersonWithInitProperties person = new PersonWithInitProperties
        {
            Name = "John Doe",
            Age = 30,
            BirthDate = new DateTime(1993, 1, 1),
            HomeAddress = new Address
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(person.Name).IsEqualTo("John Doe");
            await Assert.That(person.Age).IsEqualTo(30);
            await Assert.That(person.BirthDate).IsEqualTo(new DateTime(1993, 1, 1));
            await Assert.That(person.HomeAddress.Street).IsEqualTo("123 Main St");

            // Assert
        }
    }

    [Test]
    public async Task Test_InitOnlyProperties_WithCloning()
    {
        // Arrange
        PersonWithInitProperties original = new PersonWithInitProperties
        {
            Name = "John Doe",
            Age = 30,
            HomeAddress = new Address
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        // Act
        PersonWithInitProperties modified = original with { Age = 31 };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(modified.Name).IsEqualTo(original.Name);
            await Assert.That(modified.Age).IsEqualTo(31);
            await Assert.That(modified.HomeAddress).IsEqualTo(original.HomeAddress);
            await Assert.That(modified).IsNotSameReferenceAs(original);

            // Assert
        }
    }

    [Test]
    public async Task Test_InitOnlyProperties_RecordEquality()
    {
        // Arrange
        PersonWithInitProperties person1 = new PersonWithInitProperties
        {
            Name = "John Doe",
            Age = 30,
            HomeAddress = new Address
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        PersonWithInitProperties person2 = new PersonWithInitProperties
        {
            Name = "John Doe",
            Age = 30,
            HomeAddress = new Address
            {
                Street = "123 Main St",
                City = "New York",
                ZipCode = "10001"
            }
        };

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(person1).IsEqualTo(person2);
            await Assert.That(person1.GetHashCode()).IsEqualTo(person2.GetHashCode());
            await Assert.That(person1).IsEqualTo(person2);

            // Act & Assert
        }
    }

    public record PersonWithInitProperties
    {
        public string Name { get; init; }
        public int Age { get; init; }
        public DateTime BirthDate { get; init; }
        public Address HomeAddress { get; init; }
    }

    public record Address
    {
        public string Street { get; init; }
        public string City { get; init; }
        public string ZipCode { get; init; }
    }

    [Test]
    public async Task Test_InitOnlyProperties_WithNullValues()
    {
        // Arrange & Act
        PersonWithInitProperties person = new PersonWithInitProperties
        {
            Name = null,
            Age = 30,
            HomeAddress = null
        };

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(person.Name).IsNull();
            await Assert.That(person.Age).IsEqualTo(30);
            await Assert.That(person.HomeAddress).IsNull();

            // Assert
        }
    }

    public class CBase<TKey>
    {
        public TKey Id { get; set; }
    }

    public class C3 : CBase<int>
    {
        public new int Id { get; set; }
    }

    public class C2 : CBase<int>
    {

        public C3 C3 { get; set; } = new C3();
    }

    public class C1 : CBase<int>
    {
        public C2 C2 { get; set; } = new C2();
    }

    [Test]
    public async Task Uri_DeepClone_Test()
    {
        // Arrange
        Uri original = new Uri("https://example.com/path?query=value#fragment");

        // Act
        Uri clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.AbsoluteUri).IsEqualTo(original.AbsoluteUri);
            await Assert.That(clone.Host).IsEqualTo(original.Host);
            await Assert.That(clone.PathAndQuery).IsEqualTo(original.PathAndQuery);
            await Assert.That(clone.Fragment).IsEqualTo(original.Fragment);
            await Assert.That(clone).IsNotSameReferenceAs(original);

            // Assert
        }
    }

    [Test]
    public async Task Complex_DeepClone_Test()
    {
        // Arrange
        Complex original = new Complex(3.14, 2.718);

        // Act
        Complex clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Real).IsEqualTo(original.Real);
            await Assert.That(clone.Imaginary).IsEqualTo(original.Imaginary);
            await Assert.That(clone.Magnitude).IsEqualTo(original.Magnitude);
            await Assert.That(clone.Phase).IsEqualTo(original.Phase);

            // Assert
        }
    }

    [Test]
    public async Task BigInteger_DeepClone_Test()
    {
        // Arrange
        BigInteger? original = BigInteger.Parse("123456789012345678901234567890");

        // Act
        BigInteger? clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsEqualTo(original);
            await Assert.That(clone.ToString()).IsEqualTo("123456789012345678901234567890");
            await Assert.That((-clone).ToString()).IsEqualTo("-123456789012345678901234567890");

            // Assert
        }
    }

    [Test]
    public async Task BigInteger_DeepClone_EdgeCases_Test()
    {
        // Arrange
        BigInteger[] originals =
        [
            BigInteger.Zero,
            BigInteger.One,
            BigInteger.MinusOne,
            BigInteger.Parse("-340282366920938463463374607431768211456"),
            BigInteger.Parse("340282366920938463463374607431768211455")
        ];

        // Act & Assert
        foreach (BigInteger original in originals)
        {
            BigInteger clone = original.DeepClone();
            await Assert.That(clone).IsEqualTo(original).Because($"Failed for value: {original}");
        }
    }

    [Test]
    public async Task Version_DeepClone_Test()
    {
        // Arrange
        Version original = new Version(1, 2, 3, 4);

        // Act
        Version clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Major).IsEqualTo(original.Major);
            await Assert.That(clone.Minor).IsEqualTo(original.Minor);
            await Assert.That(clone.Build).IsEqualTo(original.Build);
            await Assert.That(clone.Revision).IsEqualTo(original.Revision);
            await Assert.That(clone).IsNotSameReferenceAs(original);

            // Assert
        }
    }

    class ValTupleTest
    {
        public int Val { get; set; }
    }

    [Test]
    public async Task ValueTuple_Simple_DeepClone_Test()
    {
        // Arrange
        (int X, string Y) original = (X: 42, Y: "test");

        // Act
        (int X, string Y) clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.X).IsEqualTo(original.X);
            await Assert.That(clone.Y).IsEqualTo(original.Y);

            // Assert
        }
    }

    [Test]
    public async Task ValueTuple_Simple_DeepClone_Test2()
    {
        ValTupleTest valX = new ValTupleTest { Val = 42 };

        // Arrange
        (ValTupleTest X, ValTupleTest Y) original = (X: valX, Y: new ValTupleTest { Val = 43 });

        // Act
        (ValTupleTest X, ValTupleTest Y) clone = original.DeepClone();
        (ValTupleTest X, ValTupleTest Y) shallow = original.ShallowClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.X.Val).IsEqualTo(original.X.Val);
            await Assert.That(clone.Y.Val).IsEqualTo(original.Y.Val);

            // Assert
        }

        valX.Val = 80;

        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(original.X, clone.X)).IsFalse();
            await Assert.That(original.X.Val).IsEqualTo(80);
            await Assert.That(clone.X.Val).IsEqualTo(42);
            await Assert.That(shallow.X.Val).IsEqualTo(80);

        }
    }

    [Test]
    public async Task ValueTuple_WithReferenceType_DeepClone_Test()
    {
        // Arrange
        List<int> list = [1, 2, 3];
        (int X, List<int> List) original = (X: 42, List: list);

        // Act
        (int X, List<int> List) clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.X).IsEqualTo(original.X);
            await Assert.That(clone.List).IsEquivalentTo(original.List);
            await Assert.That(clone.List).IsNotSameReferenceAs(original.List);

            // Assert
        }
    }

    [Test]
    public async Task ValueTuple_Nested_DeepClone_Test()
    {
        // Arrange
        (int A, string B) nested = (A: 1, B: "inner");
        ((int A, string B) X, string Y) original = (X: nested, Y: "outer");

        // Act
        ((int A, string B) X, string Y) clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.X.A).IsEqualTo(original.X.A);
            await Assert.That(clone.X.B).IsEqualTo(original.X.B);
            await Assert.That(clone.Y).IsEqualTo(original.Y);

            // Assert
        }
    }

    [Test]
    public async Task ValueTuple_WithComplexType_DeepClone_Test()
    {
        // Arrange
        Uri uri = new Uri("https://example.com");
        (int Id, Uri Uri) original = (Id: 1, Uri: uri);

        // Act
        (int Id, Uri Uri) clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Id).IsEqualTo(original.Id);
            await Assert.That(clone.Uri.AbsoluteUri).IsEqualTo(original.Uri.AbsoluteUri);
            await Assert.That(clone.Uri).IsNotSameReferenceAs(original.Uri);

            // Assert
        }
    }

    [Test]
    public async Task ValueTuple_Mutability_Test()
    {
        // Arrange
        List<int> list = [1, 2, 3];
        (int X, List<int> List) original = (X: 42, List: list);
        (int X, List<int> List) clone = original.DeepClone();

        // Act
        clone.X = 100;
        clone.List.Add(4);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(original.X).IsEqualTo(42);
            await Assert.That(original.List).Count().IsEqualTo(3);

            await Assert.That(clone.X).IsEqualTo(100);
            await Assert.That(clone.List).Count().IsEqualTo(4);

            // Assert
        }
    }

    [Test]
    public async Task Range_DeepClone_Test()
    {
        // Arrange
        Range original = new Range(Index.FromStart(1), Index.FromEnd(5));

        // Act
        Range clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Start.Value).IsEqualTo(original.Start.Value);
            await Assert.That(clone.Start.IsFromEnd).IsEqualTo(original.Start.IsFromEnd);
            await Assert.That(clone.End.Value).IsEqualTo(original.End.Value);
            await Assert.That(clone.End.IsFromEnd).IsEqualTo(original.End.IsFromEnd);
            await Assert.That(clone).IsEqualTo(original);

            // Assert
        }
    }

    [Test]
    public async Task Index_DeepClone_Test()
    {
        // Arrange
        Index original = new Index(42, fromEnd: true);

        // Act
        Index clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Value).IsEqualTo(original.Value);
            await Assert.That(clone.IsFromEnd).IsEqualTo(original.IsFromEnd);
            await Assert.That(clone).IsEqualTo(original);

            // Assert
        }
    }

    [Test]
    public async Task Index_DeepClone_FromStart_Test()
    {
        // Arrange
        Index original = Index.FromStart(10);

        // Act
        Index clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Value).IsEqualTo(original.Value);
            await Assert.That(clone.IsFromEnd).IsFalse();
            await Assert.That(clone).IsEqualTo(original);

            // Assert
        }
    }

    [Test]
    public async Task Index_DeepClone_FromEnd_Test()
    {
        // Arrange
        Index original = Index.FromEnd(10);

        // Act
        Index clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.Value).IsEqualTo(original.Value);
            await Assert.That(clone.IsFromEnd).IsTrue();
            await Assert.That(clone).IsEqualTo(original);

            // Assert
        }
    }

    [Test]
    public async Task Test_DeepClone_ClassHierarchy()
    {
        // Arrange
        C1 original = new C1
        {
            Id = 1,
            C2 = new C2
            {
                Id = 2,
                C3 = new C3
                {
                    Id = 3
                }
            }
        };

        // Act
        C1 cloned1 = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned1).IsNotSameReferenceAs(original).Because("Cloned object should be a new instance");
            await Assert.That(cloned1.Id).IsEqualTo(original.Id);
            await Assert.That(cloned1.C2).IsNotSameReferenceAs(original.C2);
            await Assert.That(cloned1.C2.Id).IsEqualTo(original.C2.Id);
            await Assert.That(cloned1.C2.C3).IsNotSameReferenceAs(original.C2.C3);
            await Assert.That(cloned1.C2.C3.Id).IsEqualTo(original.C2.C3.Id);

            // Assert
        }
    }

    private class TestProps
    {
        public int A { get; set; } = 10;
        public string B { get; set; } = "My string";
    }

    private class TestPropsWithIgnored
    {
        public int A { get; set; } = 10;

        [FastClonerIgnore]
        public string B { get; set; } = "My string";
    }
    
    private class TestPropsWithNonSerialized
    {
        public int A { get; set; } = 10;

        [NonSerialized]
        public string B = "My string";
    }

    [Test]
    public async Task Test_Clone_Props()
    {
        TestProps original = new TestProps { A = 42, B = "Test value" };
        TestProps clone = original.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(clone.A).IsEqualTo(42);
            await Assert.That(clone.B).IsEqualTo("Test value");
            await Assert.That(clone).IsNotSameReferenceAs(original);

        }
    }

    [Test]
    public async Task Test_Clone_Props_With_Ignored()
    {
        TestPropsWithIgnored original = new TestPropsWithIgnored { A = 42, B = "Test value" };
        TestPropsWithIgnored clone = original.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(clone.A).IsEqualTo(42);
            await Assert.That(clone.B).IsEqualTo(null); // default value
            await Assert.That(clone).IsNotSameReferenceAs(original);

        }
    }
    
    [Test]
    public async Task Test_Clone_Props_With_NonSerialized()
    {
        TestPropsWithNonSerialized original = new TestPropsWithNonSerialized { A = 42, B = "Test value" };
        TestPropsWithNonSerialized clone = original.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(clone.A).IsEqualTo(42);
            await Assert.That(clone.B).IsEqualTo(null); // default value
            await Assert.That(clone).IsNotSameReferenceAs(original);

        }
    }

    private class TestAutoProps
    {
        public int A { get; set; } = 10;
        public string B { get; private set; } = "My string";
        public int C => A * 2;

        private int d;

        public int D
        {
            get => d;
            set => d = value;
        }
    }

    [Test]
    public async Task Test_Clone_Auto_Properties()
    {
        // Arrange
        TestAutoProps original = new TestAutoProps
        {
            A = 42,
            D = 100
        };

        // Set private setter property via reflection
        original.GetType().GetProperty("B")!
            .SetValue(original, "Test value", null);

        // Act
        TestAutoProps clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.A).IsEqualTo(42);
            await Assert.That(clone.B).IsEqualTo("Test value");
            await Assert.That(clone.C).IsEqualTo(84);
            await Assert.That(clone.D).IsEqualTo(100);
            await Assert.That(clone).IsNotSameReferenceAs(original);

            // Assert
        }
    }

    [Test]
    public async Task ParallelCloning_WithReadOnlyFields_ShouldBeThreadSafe()
    {
        // Arrange
        ClassWithReadOnlyField testObject = new ClassWithReadOnlyField();
        const int iterations = 1000;
        ConcurrentBag<Exception> exceptions = [];

        // Act
        Parallel.For(0, iterations, i =>
        {
            try
            {
                ClassWithReadOnlyField clone = testObject.DeepClone();
                if (ReferenceEquals(clone, testObject))
                {
                    throw new InvalidOperationException("Clone should not share the original reference.");
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        await Assert.That(exceptions).IsEmpty().Because("Parallel cloning should not throw any exceptions");
    }

    private class ClassWithReadOnlyField
    {
        private readonly string readOnlyField = "test";
        public string ReadOnlyValue => readOnlyField;
    }


    private class TestAutoPropsWithIgnored
    {
        public int A { get; set; } = 10;

        [FastClonerIgnore]
        public string B { get; private set; } = "My string";

        public int C => A * 2;

        private int d;

        [FastClonerIgnore]
        public int D
        {
            get => d;
            set => d = value;
        }
    }

    [Test]
    public async Task Test_Clone_Auto_Properties_With_Ignored()
    {
        // Arrange
        TestAutoPropsWithIgnored original = new TestAutoPropsWithIgnored
        {
            A = 42,
            D = 100
        };
        original.GetType().GetProperty("B")!
            .SetValue(original, "Test value", null);

        // Act
        TestAutoPropsWithIgnored clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone.A).IsEqualTo(42);
            await Assert.That(clone.B).IsEqualTo(null);
            await Assert.That(clone.C).IsEqualTo(84);
            await Assert.That(clone.D).IsEqualTo(0);
            await Assert.That(clone).IsNotSameReferenceAs(original);

            // Assert
        }
    }

    [Test]
    public async Task Test_ExpressionTree_OrderBy1()
    {
        IOrderedQueryable<int> q = Enumerable.Range(1, 5).Reverse().AsQueryable().OrderBy(x => x);
        IOrderedQueryable<int> q2 = q.DeepClone();
        await Assert.That(q2.ToArray()[0]).IsEqualTo(1);
        await Assert.That(q.ToArray().Length).IsEqualTo(5);
    }

    [Test]
    public async Task Test_Action_Delegate_Clone()
    {
        // Arrange
        TestClass testObject = new TestClass();
        Action<string> originalAction = testObject.TestMethod;

        // Act
        Action<string> clonedAction = originalAction.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clonedAction.Target).IsSameReferenceAs(originalAction.Target).Because("Delegate Target should remain the same reference");
            await Assert.That(clonedAction.Method).IsEqualTo(originalAction.Method).Because("Delegate Method should be the same");

            // Assert
        }

        List<string> originalResult = [];
        List<string> clonedResult = [];

        originalAction("test");
        clonedAction("test");
        await Assert.That(clonedResult).IsEquivalentTo(originalResult).Because("Both delegates should produce the same result");
    }

    [Test]
    public async Task ConditionalWeakTable_DeepClone_VerifyBehavior()
    {
        ConditionalWeakTable<string, string> cwt = new ConditionalWeakTable<string, string>();
        string key = "key";
        string val = "value";
        cwt.Add(key, val);
        
        ConditionalWeakTable<string, string> clone = cwt.DeepClone();
        
        await Assert.That(clone).IsNotSameReferenceAs(cwt);

        if (clone.TryGetValue(key, out string? clonedVal))
        {
            Console.WriteLine("Clone found key: " + clonedVal);
        }
        else
        {
            Console.WriteLine("Clone did NOT find key");
        }
    }

    [Test]
    public async Task WeakReferenceGeneric_DeepClone_VerifyBehavior()
    {
        string target = "target";
        WeakReference<string> weak = new WeakReference<string>(target);
        
        WeakReference<string> clone = weak.DeepClone();
        
        await Assert.That(clone).IsSameReferenceAs(weak);

        bool hasTarget = clone.TryGetTarget(out string? clonedTarget);
        
        Console.WriteLine($"Original target alive: {weak.TryGetTarget(out _)}");
        Console.WriteLine($"Clone target alive: {hasTarget}");
    }

    [Test]
    public async Task CancellationTokenSource_DeepClone_VerifyBehavior()
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        CancellationTokenSource clone = cts.DeepClone();
        await Assert.That(clone).IsSameReferenceAs(cts);
    }

    [Test]
    public async Task CancellationTokenSource_DeepClone_VerifySafety()
    {
        // CancellationTokenSource manages native handles and cannot be safely deep cloned by value.
        // It is now treated as a "Safe" type, meaning DeepClone returns the SAME instance.
        
        using CancellationTokenSource cts = new CancellationTokenSource();
        CancellationTokenSource clone = cts.DeepClone();
        
        // Assert it is the SAME object (Reference Copy)
        await Assert.That(clone).IsSameReferenceAs(cts);
    }
    [Test]
    public async Task Test_Static_Action_Delegate_Clone()
    {
        // Arrange
        Action<string> originalAction = StaticTestMethod;

        // Act
        Action<string> clonedAction = originalAction.DeepClone();
        using (Assert.Multiple())
        {

            // Assert
            await Assert.That(clonedAction.Target).IsNull().Because("Static delegate Target should be null");
            await Assert.That(originalAction.Target).IsNull().Because("Static delegate Target should be null");
            await Assert.That(clonedAction.Method).IsEqualTo(originalAction.Method).Because("Delegate Method should be the same");
        }
    }

    [Test]
    public async Task Nested_Closure_Clone()
    {
        // Arrange
        int x = 1;

        Func<int> outer = CreateClosure();

        // Act
        Func<int> outerCopy = outer.DeepClone();

        using (Assert.Multiple())
        {
            // Assert
            await Assert.That(outer.Invoke()).IsEqualTo(6); // 1 + 3 + 2
            await Assert.That(outerCopy.Invoke()).IsEqualTo(6);

        }
        return;

        // Helper method to create closure
        Func<int> CreateClosure()
        {
            int y = 3;
            int z = 2;
            return () => x + y + z;
        }
    }

    [Test]
    public async Task Event_Handler_Clone_With_Method()
    {
        // Arrange
        EventSource source = new EventSource();
        EventListener listener = new EventListener();
        EventHandler handler = listener.HandleEvent;
        source.TestEvent += handler;

        // Act
        EventHandler handlerCopy = handler.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(handlerCopy.Target).IsSameReferenceAs(handler.Target).Because("Handler Target should be the same");
            await Assert.That(handlerCopy.Method).IsEqualTo(handler.Method).Because("Handler Method should be the same");

            source.RaiseEvent();
            await Assert.That(listener.Counter).IsEqualTo(1).Because("Original handler should increment counter");

            source.TestEvent += handlerCopy;
            source.RaiseEvent();
            await Assert.That(listener.Counter).IsEqualTo(3).Because("Both handlers should increment counter");

            // Assert
        }
    }

    private class EventListener
    {
        public int Counter { get; private set; }

        public void HandleEvent(object sender, EventArgs e)
        {
            Counter++;
        }
    }

    private class EventSource
    {
        public event EventHandler TestEvent;

        public void RaiseEvent()
        {
            TestEvent?.Invoke(this, EventArgs.Empty);
        }
    }


    private static void StaticTestMethod(string input)
    {
        Console.WriteLine(input);
    }

    private class TestClass
    {
        public void TestMethod(string input)
        {
            Console.WriteLine(input);
        }
    }

    [Test]
    public async Task Circular_Reference_Clone()
    {
        // Arrange
        CircularClass original = new CircularClass
        {
            Name = "Test"
        };

        original.Reference = original;

        // Act
        CircularClass cloned = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Cloned object should be a new instance");
            await Assert.That(cloned.Name).IsEqualTo(original.Name).Because("Properties should be copied");
            await Assert.That(cloned.Reference).IsSameReferenceAs(cloned).Because("Circular reference should point to the cloned instance");
            await Assert.That(cloned.Reference.Reference).IsSameReferenceAs(cloned).Because("Nested circular reference should point to the cloned instance");

            // Assert
        }
    }

    private class CircularClass
    {
        public string Name { get; set; }
        public CircularClass Reference { get; set; }
    }

    [Test]
    public async Task Complex_Circular_Reference_Clone()
    {
        // Arrange
        Node nodeA = new Node { Name = "A" };
        Node nodeB = new Node { Name = "B" };
        Node nodeC = new Node { Name = "C" };

        // A -> B -> C -> A
        nodeA.Next = nodeB;
        nodeB.Next = nodeC;
        nodeC.Next = nodeA;

        // Act
        Node clonedA = nodeA.DeepClone();
        Node clonedB = clonedA.Next;
        Node clonedC = clonedB.Next;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clonedA).IsNotSameReferenceAs(nodeA).Because("Node A should be cloned");
            await Assert.That(clonedB).IsNotSameReferenceAs(nodeB).Because("Node B should be cloned");
            await Assert.That(clonedC).IsNotSameReferenceAs(nodeC).Because("Node C should be cloned");

            await Assert.That(clonedA.Name).IsEqualTo("A").Because("Node A name should be copied");
            await Assert.That(clonedB.Name).IsEqualTo("B").Because("Node B name should be copied");
            await Assert.That(clonedC.Name).IsEqualTo("C").Because("Node C name should be copied");

            await Assert.That(clonedC.Next).IsSameReferenceAs(clonedA).Because("Cycle should be preserved");
            await Assert.That(clonedA.Next).IsSameReferenceAs(clonedB).Because("References should point to new instances");
            await Assert.That(clonedB.Next).IsSameReferenceAs(clonedC).Because("References should point to new instances");

            // Assert
        }
    }

    [Test]
    public async Task Dynamic_Object_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.Name = "Test";
        original.Number = 42;
        original.Nested = new ExpandoObject();
        original.Nested.Value = "Nested Value";

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object?)cloned).IsNotSameReferenceAs((object?)original).Because("Cloned object should be a new instance");
            await Assert.That((string)cloned.Name).IsEqualTo("Test").Because("String property should be copied");
            await Assert.That((int)cloned.Number).IsEqualTo(42).Because("Number property should be copied");
            await Assert.That((object?)cloned.Nested).IsNotSameReferenceAs((object?)original.Nested).Because("Nested object should be cloned");
            await Assert.That((string)cloned.Nested.Value).IsEqualTo("Nested Value").Because("Nested value should be copied");

            // Assert
        }
    }

    [Test]
    public async Task Dynamic_With_Nested_ExpandoObject_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.Name = "Parent";
        original.Child = new ExpandoObject();
        original.Child.Name = "Child";
        original.Child.Parent = original; // Circular reference

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((string)cloned.Name).IsEqualTo("Parent").Because("Parent name should be copied");
            await Assert.That((string)cloned.Child.Name).IsEqualTo("Child").Because("Child name should be copied");

            await Assert.That((object?)cloned.Child.Parent).IsSameReferenceAs((object?)cloned).Because("Circular reference should point to cloned parent");
            await Assert.That((object?)original.Child.Parent).IsSameReferenceAs((object?)original).Because("Original circular reference should remain unchanged");

            // Assert
        }
    }

    [Test]
    public async Task Dynamic_With_Collection_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.Items = new List<ExpandoObject>();

        dynamic item1 = new ExpandoObject();
        item1.Name = "Item1";
        item1.Owner = original;

        dynamic item2 = new ExpandoObject();
        item2.Name = "Item2";
        item2.Owner = original;

        original.Items.Add(item1);
        original.Items.Add(item2);

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object?)cloned.Items).IsNotSameReferenceAs((object?)original.Items).Because("Collection should be cloned");
            await Assert.That((int)cloned.Items.Count).IsEqualTo(2).Because("Collection should have same number of items");

            await Assert.That((string)cloned.Items[0].Name).IsEqualTo("Item1").Because("First item name should be copied");
            await Assert.That((object?)cloned.Items[0].Owner).IsSameReferenceAs((object?)cloned).Because("First item should reference cloned parent");

            await Assert.That((string)cloned.Items[1].Name).IsEqualTo("Item2").Because("Second item name should be copied");
            await Assert.That((object?)cloned.Items[1].Owner).IsSameReferenceAs((object?)cloned).Because("Second item should reference cloned parent");

            // Assert
        }
    }

    [Test]
    public async Task HttpRequest_Clone()
    {
        // Arrange
        HttpRequestMessage original = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.example.com/data"),
            Version = new Version(2, 0),
            Content = new StringContent(
                "{\"key\":\"value\"}",
                Encoding.UTF8,
                "application/json")
        };

        original.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        original.Headers.Add("Custom-Header", "test-value");
        original.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        // Act
        HttpRequestMessage? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Method).IsEqualTo(HttpMethod.Post).Because("Method should be copied");
            await Assert.That(cloned.RequestUri?.ToString()).IsEqualTo("https://api.example.com/data").Because("URI should be copied");
            await Assert.That(cloned.Version).IsEqualTo(new Version(2, 0)).Because("Version should be copied");

            await Assert.That(cloned.Headers.Accept.First().MediaType).IsEqualTo("application/json").Because("Accept header should be copied");
            await Assert.That(cloned.Headers.GetValues("Custom-Header").First()).IsEqualTo("test-value").Because("Custom header should be copied");
            await Assert.That(cloned.Headers.Authorization?.Scheme).IsEqualTo("Bearer").Because("Authorization scheme should be copied");
            await Assert.That(cloned.Headers.Authorization?.Parameter).IsEqualTo("test-token").Because("Authorization parameter should be copied");

            await Assert.That(cloned.Content).IsNotNull().Because("Content should be cloned");
            await Assert.That(cloned.Content).IsTypeOf<StringContent>().Because("Content type should be preserved");

            string originalContent = original.Content.ReadAsStringAsync().Result;
            string clonedContent = cloned.Content.ReadAsStringAsync().Result;
            await Assert.That(clonedContent).IsEqualTo(originalContent).Because("Content value should be copied");
            await Assert.That(cloned.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json").Because("Content-Type should be copied");

            // Assert
        }
    }

    [Test]
    public async Task HttpRequest_With_MultipartContent_Clone()
    {
        // Arrange
        HttpRequestMessage original = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://api.example.com/upload")
        };

        MultipartFormDataContent multipartContent = new MultipartFormDataContent();

        StringContent stringContent = new StringContent("text data", Encoding.UTF8);
        multipartContent.Add(stringContent, "text");

        byte[] binaryData = "binary data"u8.ToArray();
        ByteArrayContent byteContent = new ByteArrayContent(binaryData);
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipartContent.Add(byteContent, "file", "test.bin");

        original.Content = multipartContent;

        // Act
        HttpRequestMessage? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Content).IsTypeOf<MultipartFormDataContent>().Because("Content type should be preserved");

            MultipartFormDataContent? originalMultipart = (MultipartFormDataContent)original.Content;
            MultipartFormDataContent? clonedMultipart = (MultipartFormDataContent)cloned.Content;

            string originalParts = originalMultipart.ReadAsStringAsync().Result;
            string clonedParts = clonedMultipart.ReadAsStringAsync().Result;

            await Assert.That(clonedParts).IsEqualTo(originalParts).Because("Multipart content should be identical");
            await Assert.That(clonedMultipart.Headers.ContentType?.Parameters.First(p => p.Name == "boundary").Value).IsNotNull().Because("Boundary should be present");

            // Assert
        }
    }

    [Test]
    public async Task HttpRequest_With_Handlers_Clone()
    {
        // Arrange
        HttpRequestMessage original = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com");
        HttpClientHandler handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = false
        };

        original.Properties.Add("AllowAutoRedirect", handler.AllowAutoRedirect);
        original.Properties.Add("AutomaticDecompression", handler.AutomaticDecompression);
        original.Properties.Add("UseCookies", handler.UseCookies);

        HttpRequestMessage? cloned = FastCloner.DeepClone(original);

        using (Assert.Multiple())
        {
            await Assert.That(cloned.Properties).IsNotEmpty().Because("Properties should be copied");
            await Assert.That(cloned.Properties["AllowAutoRedirect"]).IsEqualTo(false).Because("Handler property should be copied");
            await Assert.That(cloned.Properties["AutomaticDecompression"]).IsEqualTo(DecompressionMethods.GZip | DecompressionMethods.Deflate).Because("Handler compression settings should be copied");
            await Assert.That(cloned.Properties["UseCookies"]).IsEqualTo(false).Because("Handler cookie settings should be copied");

        }
    }

    [Test]
    public async Task HttpResponse_Clone()
    {
        // Arrange
        HttpResponseMessage original = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Version = new Version(2, 0),
            Content = new StringContent(
                "{\"result\":\"success\"}",
                Encoding.UTF8,
                "application/json"),
            ReasonPhrase = "Custom OK Message"
        };

        original.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
        original.Headers.Add("X-Custom-Response", "test-response");

        // Act
        HttpResponseMessage? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.StatusCode).IsEqualTo(HttpStatusCode.OK).Because("Status code should be copied");
            await Assert.That(cloned.Version).IsEqualTo(new Version(2, 0)).Because("Version should be copied");
            await Assert.That(cloned.ReasonPhrase).IsEqualTo("Custom OK Message").Because("Reason phrase should be copied");

            await Assert.That(cloned.Headers.CacheControl?.MaxAge).IsEqualTo(TimeSpan.FromHours(1)).Because("Cache control should be copied");
            await Assert.That(cloned.Headers.GetValues("X-Custom-Response").First()).IsEqualTo("test-response").Because("Custom header should be copied");

            string originalContent = original.Content.ReadAsStringAsync().Result;
            string clonedContent = cloned.Content.ReadAsStringAsync().Result;
            await Assert.That(clonedContent).IsEqualTo(originalContent).Because("Content should be copied");

            // Assert
        }
    }

    [Test]
    public async Task Font_Clone()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Arrange
        Font original = new Font("Arial", 12, FontStyle.Bold | FontStyle.Italic);

        // Act
        Font? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should be different instance");
            await Assert.That(cloned.Name).IsEqualTo("Arial").Because("Font name should be copied");
            await Assert.That(cloned.Size).IsEqualTo(12).Because("Font size should be copied");
            await Assert.That(cloned.Style).IsEqualTo(FontStyle.Bold | FontStyle.Italic).Because("Font style should be copied");
            await Assert.That(cloned.Unit).IsEqualTo(original.Unit).Because("Font unit should be copied");
            await Assert.That(cloned.GdiCharSet).IsEqualTo(original.GdiCharSet).Because("GDI charset should be copied");
            await Assert.That(cloned.GdiVerticalFont).IsEqualTo(original.GdiVerticalFont).Because("GDI vertical font should be copied");

            // Assert
        }
    }


    [Test]
    public async Task HttpRequest_With_StreamContent_Clone()
    {
        // Arrange
        HttpRequestMessage original = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/stream");
        MemoryStream streamData = new MemoryStream("stream test data"u8.ToArray());
        StreamContent streamContent = new StreamContent(streamData);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        original.Content = streamContent;

        // Act
        HttpRequestMessage? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Content).IsTypeOf<StreamContent>().Because("Content type should be preserved");

            string originalContent = original.Content.ReadAsStringAsync().Result;
            string clonedContent = cloned.Content.ReadAsStringAsync().Result;
            await Assert.That(clonedContent).IsEqualTo(originalContent).Because("Stream content should be copied");
            await Assert.That(cloned.Content.Headers.ContentType?.MediaType).IsEqualTo("text/plain").Because("Content type should be copied");

            // Assert
        }
    }

    [Test]
    public async Task HttpRequest_With_ComplexHeaders_Clone()
    {
        // Arrange
        HttpRequestMessage original = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com");

        original.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 1.0));
        original.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml", 0.8));

        original.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 1.0));
        original.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("cs-CZ", 0.8));

        original.Headers.Add("If-Match", ["\"123\"", "\"456\""]);
        original.Headers.Add("X-Custom-Multi", ["value1", "value2"]);

        // Act
        HttpRequestMessage? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            List<MediaTypeWithQualityHeaderValue> acceptHeaders = cloned.Headers.Accept.OrderBy(x => x.MediaType).ToList();
            await Assert.That(acceptHeaders[0].MediaType).IsEqualTo("application/json").Because("First accept header should be copied");
            await Assert.That(acceptHeaders[0].Quality).IsEqualTo(1.0).Because("First accept header quality should be copied");
            await Assert.That(acceptHeaders[1].MediaType).IsEqualTo("text/xml").Because("Second accept header should be copied");
            await Assert.That(acceptHeaders[1].Quality).IsEqualTo(0.8).Because("Second accept header quality should be copied");

            List<StringWithQualityHeaderValue> languageHeaders = cloned.Headers.AcceptLanguage.OrderBy(x => x.Value).ToList();
            await Assert.That(languageHeaders[0].Value).IsEqualTo("cs-CZ").Because("First language header should be copied");
            await Assert.That(languageHeaders[0].Quality).IsEqualTo(0.8).Because("First language header quality should be copied");
            await Assert.That(languageHeaders[1].Value).IsEqualTo("en-US").Because("Second language header should be copied");
            await Assert.That(languageHeaders[1].Quality).IsEqualTo(1.0).Because("Second language header quality should be copied");

            List<string> ifMatchValues = cloned.Headers.GetValues("If-Match").ToList();
            await Assert.That(ifMatchValues).Count().IsEqualTo(2).Because("If-Match headers count should match");
            await Assert.That(ifMatchValues).Contains("\"123\"").Because("First If-Match value should be copied");
            await Assert.That(ifMatchValues).Contains("\"456\"").Because("Second If-Match value should be copied");

            List<string> customMultiValues = cloned.Headers.GetValues("X-Custom-Multi").ToList();
            await Assert.That(customMultiValues).Count().IsEqualTo(2).Because("Custom multi-value header count should match");
            await Assert.That(customMultiValues).Contains("value1").Because("First custom multi-value should be copied");
            await Assert.That(customMultiValues).Contains("value2").Because("Second custom multi-value should be copied");

            // Assert
        }
    }

    [Test]
    public async Task Dynamic_With_Dictionary_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.Dict = new Dictionary<string, ExpandoObject>();

        dynamic value1 = new ExpandoObject();
        value1.Name = "Value1";
        value1.Container = original;

        original.Dict["key1"] = value1;
        original.Self = original;

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object?)cloned.Dict).IsNotSameReferenceAs((object?)original.Dict).Because("Dictionary should be cloned");
            await Assert.That((int)cloned.Dict.Count).IsEqualTo(1).Because("Dictionary should have same number of items");
            await Assert.That((string)cloned.Dict["key1"].Name).IsEqualTo("Value1").Because("Dictionary value should be copied");
            await Assert.That((object?)cloned.Dict["key1"].Container).IsSameReferenceAs((object?)cloned).Because("Dictionary value should reference cloned container");
            await Assert.That((object?)cloned.Self).IsSameReferenceAs((object?)cloned).Because("Self reference should point to clone");

            // Assert
        }
    }

    [Test]
    public async Task NotifyPropertyChanged_Clone()
    {
        // Arrange
        NotifyingPerson original = new NotifyingPerson { Name = "John", Age = 30 };
        List<string> propertyChanges = [];
        original.PropertyChanged += (sender, args) => propertyChanges.Add(args.PropertyName);

        // Act
        NotifyingPerson? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Name).IsEqualTo("John").Because("Property should be copied");
            await Assert.That(cloned.Age).IsEqualTo(30).Because("Property should be copied");

            cloned.Name = "Jane";
            await Assert.That(propertyChanges).Count().IsEqualTo(1).Because("Cloned object should trigger original events due to shallow copy of delegates");
            await Assert.That(propertyChanges[0]).IsEqualTo("Name");

            List<string> clonedChanges = [];
            cloned.PropertyChanged += (object sender, PropertyChangedEventArgs args) => clonedChanges.Add(args.PropertyName);
            cloned.Age = 31;
            await Assert.That(clonedChanges).Count().IsEqualTo(1).Because("Cloned object should trigger its own events");
            await Assert.That(propertyChanges).Count().IsEqualTo(2).Because("Original event handler also receives the second change");

            // Assert
        }
    }

    [Test]
    public async Task NotifyPropertyChanged_With_Complex_Properties_Clone()
    {
        // Arrange
        NotifyingPerson original = new NotifyingPerson
        {
            Name = "John",
            Address = new NotifyingAddress { Street = "Main St", City = "New York" }
        };

        List<string> addressChanges = [];
        original.Address.PropertyChanged += (object sender, PropertyChangedEventArgs args) => addressChanges.Add(args.PropertyName);

        // Act
        NotifyingPerson? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Address).IsNotNull().Because("Complex property should be cloned");
            await Assert.That(cloned.Address.Street).IsEqualTo("Main St").Because("Nested property should be copied");
            await Assert.That(cloned.Address.City).IsEqualTo("New York").Because("Nested property should be copied");

            cloned.Address.Street = "Broadway";
            await Assert.That(addressChanges).Count().IsEqualTo(1).Because("Delegates are shallow-copied, so cloned nested object triggers original handler");
            await Assert.That(addressChanges[0]).IsEqualTo(nameof(NotifyingAddress.Street));

            List<string> clonedAddressChanges = [];
            cloned.Address.PropertyChanged += (object sender, PropertyChangedEventArgs args) => clonedAddressChanges.Add(args.PropertyName);
            cloned.Address.City = "Boston";
            await Assert.That(clonedAddressChanges).Count().IsEqualTo(1).Because("Cloned nested object should trigger its own events");
            await Assert.That(addressChanges).Count().IsEqualTo(2).Because("Original handler also receives the second change");

            // Assert
        }
    }

    [Test]
    public async Task NotifyPropertyChanged_With_Collection_Clone()
    {
        // Arrange
        NotifyingPerson original = new NotifyingPerson
        {
            Name = "John",
            Children =
            [
                new NotifyingPerson { Name = "Child1", Age = 5 },
                new NotifyingPerson { Name = "Child2", Age = 7 }
            ]
        };

        int collectionChanges = 0;
        original.Children.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs args) => collectionChanges++;

        // Act
        NotifyingPerson? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Children).IsNotNull().Because("Collection should be cloned");
            await Assert.That(cloned.Children).Count().IsEqualTo(2).Because("Collection should have same number of items");
            await Assert.That(cloned.Children[0].Name).IsEqualTo("Child1").Because("Collection items should be copied");

            int clonedChanges = 0;
            cloned.Children.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs args) => clonedChanges++;
            cloned.Children.Add(new NotifyingPerson { Name = "Child3" });
            await Assert.That(clonedChanges).IsEqualTo(1).Because("Cloned collection should trigger its own events");

            cloned.Children.RemoveAt(0);
            await Assert.That(clonedChanges).IsEqualTo(2).Because("Cloned collection should continue triggering its own events");

            // Assert
        }
    }

    public class NotifyTest : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string prop;

        public string Prop
        {
            get => prop;

            set
            {
                prop = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Prop)));
            }
        }
    }

    private unsafe class UnnamedTypeContainer
    {
        public int Value;
        public object? Object;
        public delegate*<IServiceProvider, object> Builder;
    }

    [Test]
    public unsafe void Test_Unnamed_Type()
    {
        // Arrange
        int[] array = [1, 2, 3];
        IntPtr builder = (IntPtr)GCHandle.Alloc(array, GCHandleType.Pinned);
        UnnamedTypeContainer obj = new UnnamedTypeContainer
        {
            Value = 1,
            Object = new object(),
            Builder = (delegate*<IServiceProvider, object>)builder
        };

        // Act
        UnnamedTypeContainer result = obj.DeepClone();

        // Assert
        bool builderMatches = result.Builder == obj.Builder;

        if (ReferenceEquals(result, obj))
            throw new InvalidOperationException("Unnamed type container should be cloned.");

        if (result.Value != obj.Value)
            throw new InvalidOperationException("Unnamed type value should be preserved.");

        if (ReferenceEquals(result.Object, obj.Object))
            throw new InvalidOperationException("Unnamed type object field should be cloned.");

        if (!builderMatches)
            throw new InvalidOperationException("Unnamed type builder pointer should be preserved.");
    }

    [Test]
    public async Task Test_Rune()
    {
        // Arrange
        Rune obj = new Rune(0x1F44D);

        // Act
        Rune result = obj.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result).IsEqualTo(obj);
            await Assert.That(result).IsEqualTo(obj);
            await Assert.That(result.Value).IsEqualTo(obj.Value);
            await Assert.That(result.ToString()).IsEqualTo("👍");

            // Assert
        }
    }

    [Test]
    public async Task Test_RuneContainer()
    {
        // Arrange
        RuneContainer container = new RuneContainer
        {
            // Emoji '🚀' (ROCKET) - Unicode U+1F680
            RuneValue = new Rune(0x1F680)
        };

        // Act
        RuneContainer result = container.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(result, container)).IsFalse();
            await Assert.That(result.RuneValue).IsEqualTo(container.RuneValue);
            await Assert.That(result.RuneValue.ToString()).IsEqualTo("🚀");

            // Assert
        }
    }

    public class RuneContainer
    {
        public Rune RuneValue { get; set; }
    }

    [Test]
    public async Task Test_TimeSpan()
    {
        // Arrange
        TimeSpan obj = TimeSpan.FromHours(42.5);

        // Act
        TimeSpan result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_TimeZoneInfo()
    {
        // Arrange
        TimeZoneInfo obj = TimeZoneInfo.Local;

        // Act
        TimeZoneInfo result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_Half()
    {
        // Arrange
        Half obj = (Half)42.5f;

        // Act
        Half result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_Int128()
    {
        // Arrange
        Int128 obj = Int128.Parse("123456789012345678901234567890");

        // Act
        Int128 result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_UInt128()
    {
        // Arrange
        UInt128 obj = UInt128.Parse("123456789012345678901234567890");

        // Act
        UInt128 result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_Char()
    {
        // Arrange
        char obj = 'Ž';

        // Act
        char result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_Bool()
    {
        // Arrange
        bool obj = true;

        // Act
        bool result = obj.DeepClone();

        // Assert
        await Assert.That(result).IsEqualTo(obj);
    }

    [Test]
    public async Task Test_Notify_Triggered_Correctly()
    {
        // Arrange
        List<string> output = [];
        NotifyTest a = new NotifyTest();
        a.PropertyChanged += (sender, args) => { output.Add(((NotifyTest)sender).Prop); };

        // Act
        a.Prop = "A changed";
        NotifyTest b = a.DeepClone();
        b.Prop = "B changed";
        b.Prop = "B changed again";

        // Assert - delegates are shallow-copied, so the clone shares the original handler
        await Assert.That(output).Count().IsEqualTo(3);
        await Assert.That(output[0]).IsEqualTo("A changed");
        await Assert.That(output[1]).IsEqualTo("B changed");
        await Assert.That(output[2]).IsEqualTo("B changed again");
    }

    /// <summary>
    /// issue #27
    /// </summary>
    public class MvvmEntity : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private string name;
        private int value;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event PropertyChangingEventHandler? PropertyChanging;

        public string Name
        {
            get => name;
            set
            {
                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Name)));
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int Value
        {
            get => value;
            set
            {
                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
                this.value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public bool HasPropertyChangedHandler => PropertyChanged is not null;
        public bool HasPropertyChangingHandler => PropertyChanging is not null;
    }

    [Test]
    public async Task Issue27_Clone_Entity_With_EventHandlers_Does_Not_Deep_Clone_Delegates()
    {
        // Arrange - simulate WPF-like scenario: entity with MVVM event handlers
        MvvmEntity original = new MvvmEntity { Name = "Test", Value = 42 };

        List<string> changedProps = [];
        List<string> changingProps = [];
        original.PropertyChanged += (sender, args) => changedProps.Add(args.PropertyName!);
        original.PropertyChanging += (sender, args) => changingProps.Add(args.PropertyName!);

        // Act - clone the entity (previously would deep-clone delegate targets, hitting COM objects)
        MvvmEntity clone = original.DeepClone();

        // Assert - clone should work without exceptions
        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.Name).IsEqualTo("Test");
        await Assert.That(clone.Value).IsEqualTo(42);

        // Delegates are shallow-copied: handlers are preserved as references
        await Assert.That(clone.HasPropertyChangedHandler).IsTrue();
        await Assert.That(clone.HasPropertyChangingHandler).IsTrue();

        // Mutating the clone triggers the original handlers (shared delegate reference)
        clone.Name = "Modified";
        await Assert.That(changedProps).Count().IsEqualTo(1);
        await Assert.That(changingProps).Count().IsEqualTo(1);
        await Assert.That(changedProps[0]).IsEqualTo("Name");
        await Assert.That(changingProps[0]).IsEqualTo("Name");

        for (int i = 0; i < 100; i++)
        {
            MvvmEntity rapidClone = original.DeepClone();
            await Assert.That(rapidClone.Name).IsEqualTo("Test");
            await Assert.That(rapidClone.HasPropertyChangedHandler).IsTrue();
        }
    }

    [Test]
    [NotInParallel("FastClonerGlobalState")]
    public async Task Issue27_Clone_Entity_With_Ignored_EventHandlers_Nulls_Delegates()
    {
        // Arrange - user opts to ignore event handler types (OP's preferred workaround)
        FastCloner.SetTypeBehavior<PropertyChangedEventHandler>(CloneBehavior.Ignore);
        FastCloner.SetTypeBehavior<PropertyChangingEventHandler>(CloneBehavior.Ignore);

        try
        {
            MvvmEntity original = new MvvmEntity { Name = "Test", Value = 42 };
            original.PropertyChanged += (sender, args) => { };
            original.PropertyChanging += (sender, args) => { };

            // Act
            MvvmEntity clone = original.DeepClone();

            // Assert - handlers should be null on the clone
            await Assert.That(clone).IsNotNull();
            await Assert.That(clone.Name).IsEqualTo("Test");
            await Assert.That(clone.Value).IsEqualTo(42);
            await Assert.That(clone.HasPropertyChangedHandler).IsFalse().Because("Ignored delegate types should be null on clone");
            await Assert.That(clone.HasPropertyChangingHandler).IsFalse().Because("Ignored delegate types should be null on clone");

            // Original should still have its handlers
            await Assert.That(original.HasPropertyChangedHandler).IsTrue();
            await Assert.That(original.HasPropertyChangingHandler).IsTrue();
        }
        finally
        {
            FastCloner.ClearTypeBehavior<PropertyChangedEventHandler>();
            FastCloner.ClearTypeBehavior<PropertyChangingEventHandler>();
        }
    }

    [Test]
    [NotInParallel("FastClonerGlobalState")]
    public async Task Issue27_Clone_Entity_With_Ignored_EventHandlers_After_PreWarm_Nulls_Delegates()
    {
        MvvmEntity original = new MvvmEntity { Name = "Test", Value = 42 };
        original.PropertyChanged += (sender, args) => { };
        original.PropertyChanging += (sender, args) => { };

        // Pre-warm clone delegates under default behavior before applying overrides.
        MvvmEntity baselineClone = original.DeepClone();
        await Assert.That(baselineClone.HasPropertyChangedHandler).IsTrue();
        await Assert.That(baselineClone.HasPropertyChangingHandler).IsTrue();

        FastCloner.SetTypeBehavior<PropertyChangedEventHandler>(CloneBehavior.Ignore);
        FastCloner.SetTypeBehavior<PropertyChangingEventHandler>(CloneBehavior.Ignore);

        try
        {
            MvvmEntity clone = original.DeepClone();
            await Assert.That(clone).IsNotNull();
            await Assert.That(clone.HasPropertyChangedHandler).IsFalse().Because("Ignored delegate type should not remain from stale cache.");
            await Assert.That(clone.HasPropertyChangingHandler).IsFalse().Because("Ignored delegate type should not remain from stale cache.");
        }
        finally
        {
            FastCloner.ClearTypeBehavior<PropertyChangedEventHandler>();
            FastCloner.ClearTypeBehavior<PropertyChangingEventHandler>();
        }
    }

    public class NotifyingPerson : INotifyPropertyChanged
    {
        private string name;
        private int age;
        private NotifyingAddress address;
        private ObservableCollection<NotifyingPerson> children;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get => name;
            set
            {
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int Age
        {
            get => age;
            set
            {
                age = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age)));
            }
        }

        public NotifyingAddress Address
        {
            get => address;
            set
            {
                address = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Address)));
            }
        }

        public ObservableCollection<NotifyingPerson> Children
        {
            get => children;
            set
            {
                children = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Children)));
            }
        }
    }

    public class NotifyingAddress : INotifyPropertyChanged
    {
        private string street;
        private string city;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Street
        {
            get => street;
            set
            {
                street = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Street)));
            }
        }

        public string City
        {
            get => city;
            set
            {
                city = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(City)));
            }
        }
    }

    [Test]
    public async Task Dynamic_With_Delegate_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        int counter = 0;
        original.Name = "Test";
        original.Increment = (Func<int>)(() => ++counter);

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((string)cloned.Name).IsEqualTo("Test").Because("String property should be copied");

            // Original delegate increments the shared counter
            int originalResult = original.Increment();
            await Assert.That(originalResult).IsEqualTo(1).Because("Original delegate should increment counter");
            await Assert.That(counter).IsEqualTo(1).Because("Counter should be 1");

            // Delegates are shallow-copied, so cloned delegate shares the same closure
            int clonedResult = cloned.Increment();
            await Assert.That(clonedResult).IsEqualTo(2).Because("Cloned delegate shares the same counter");
            await Assert.That(counter).IsEqualTo(2).Because("Counter affected by both delegates");

            // Both continue on the same counter
            originalResult = original.Increment();
            clonedResult = cloned.Increment();
            await Assert.That(originalResult).IsEqualTo(3).Because("Original delegate continues counting");
            await Assert.That(clonedResult).IsEqualTo(4).Because("Cloned delegate continues on same counter");
            await Assert.That(counter).IsEqualTo(4).Because("Counter affected by both delegates");

            // Assert
        }
    }

    [Test]
    public async Task ExpandoObject_With_Collection_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.List = new List<string> { "Item1", "Item2" };
        original.Dictionary = new Dictionary<string, int> { ["Key1"] = 1, ["Key2"] = 2 };

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object?)cloned.List).IsNotSameReferenceAs((object?)original.List).Because("List should be cloned");
            await Assert.That((IEnumerable<string>)cloned.List).IsEquivalentTo((IEnumerable<string>)original.List).Because("List items should be copied");
            await Assert.That((object?)cloned.Dictionary).IsNotSameReferenceAs((object?)original.Dictionary).Because("Dictionary should be cloned");
            await Assert.That((int)cloned.Dictionary["Key1"]).IsEqualTo(1).Because("Dictionary values should be copied");
            await Assert.That((int)cloned.Dictionary["Key2"]).IsEqualTo(2).Because("Dictionary values should be copied");

            // Assert
        }
    }

    [Test]
    public async Task ReadOnlyDictionary_Clone_ShouldCreateNewInstance()
    {
        // Arrange
        Dictionary<string, int> originalDict = new Dictionary<string, int> { ["One"] = 1, ["Two"] = 2 };
        ReadOnlyDictionary<string, int> original = new ReadOnlyDictionary<string, int>(originalDict);

        // Act
        ReadOnlyDictionary<string, int>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsTypeOf<ReadOnlyDictionary<string, int>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");
            await Assert.That(cloned["One"]).IsEqualTo(1).Because("Should preserve values");
            await Assert.That(cloned["Two"]).IsEqualTo(2).Because("Should preserve values");

            // Assert
        }
    }

    [Test]
    public async Task IReadOnlyDictionary_Clone_ShouldCreateNewInstance()
    {
        // Arrange
        IReadOnlyDictionary<string, int> original =
            new Dictionary<string, int> { ["One"] = 1, ["Two"] = 2 }.AsReadOnly();

        // Act
        IReadOnlyDictionary<string, int>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<IReadOnlyDictionary<string, int>>().Because("Should preserve interface");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");
            await Assert.That(cloned["One"]).IsEqualTo(1).Because("Should preserve values");
            await Assert.That(cloned["Two"]).IsEqualTo(2).Because("Should preserve values");

            // Assert
        }
    }

    [Test]
    public async Task IReadOnlySet_Clone_ShouldCreateNewInstance()
    {
        // Arrange
        IReadOnlySet<string> original = new HashSet<string> { "One", "Two", "Three" }.AsReadOnly();

        // Act
        IReadOnlySet<string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<IReadOnlySet<string>>().Because("Should preserve interface");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");
            await Assert.That(cloned.Contains("One")).IsTrue().Because("Should contain original elements");
            await Assert.That(cloned.Contains("Two")).IsTrue().Because("Should contain original elements");
            await Assert.That(cloned.Contains("Three")).IsTrue().Because("Should contain original elements");

            // Assert
        }
    }

    [Test]
    public async Task IReadOnlySet_IsSubsetOf_ShouldWorkCorrectly()
    {
        // Arrange
        IReadOnlySet<int> original = new HashSet<int> { 1, 2 }.AsReadOnly();
        IReadOnlySet<int> superSet = new HashSet<int> { 1, 2, 3 }.AsReadOnly();
        IReadOnlySet<int> nonSuperSet = new HashSet<int> { 1, 4 }.AsReadOnly();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(original.IsSubsetOf(superSet)).IsTrue().Because("Should be subset of superset");
            await Assert.That(original.IsSubsetOf(nonSuperSet)).IsFalse().Because("Should not be subset of non-superset");
            await Assert.That(original.IsSubsetOf(original)).IsTrue().Because("Should be subset of itself");

            // Assert
        }
    }

    [Test]
    public async Task IReadOnlySet_Overlaps_ShouldWorkCorrectly()
    {
        // Arrange
        IReadOnlySet<char> setA = new HashSet<char> { 'a', 'b', 'c' }.AsReadOnly();
        IReadOnlySet<char> setB = new HashSet<char> { 'b', 'c', 'd' }.AsReadOnly();
        IReadOnlySet<char> setC = new HashSet<char> { 'x', 'y', 'z' }.AsReadOnly();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(setA.Overlaps(setB)).IsTrue().Because("Sets with common elements should overlap");
            await Assert.That(setA.Overlaps(setC)).IsFalse().Because("Sets without common elements should not overlap");
            await Assert.That(setA.Overlaps(setA)).IsTrue().Because("Set should overlap with itself");

            // Assert
        }
    }

    [Test]
    public async Task Stack_DeepClone_ShouldCreateNewInstance()
    {
        // Arrange
        Stack<string> original = new Stack<string>();
        original.Push("One");
        original.Push("Two");
        original.Push("Three");

        // Act
        Stack<string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<Stack<string>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            // Verify stack order by popping elements
            await Assert.That(cloned.Pop()).IsEqualTo("Three").Because("Top element should be preserved");
            await Assert.That(cloned.Pop()).IsEqualTo("Two").Because("Second element should be preserved");
            await Assert.That(cloned.Pop()).IsEqualTo("One").Because("Bottom element should be preserved");
            await Assert.That(cloned.Count).IsEqualTo(0).Because("Should be empty after popping all elements");

            // Assert
        }
    }

    [Test]
    public async Task Stack_DeepClone_WithComplexObjects_ShouldCreateDeepCopy()
    {
        // Arrange
        Person complexObj1 = new Person { Name = "Alice", Age = 30 };
        Person complexObj2 = new Person { Name = "Bob", Age = 25 };

        Stack<Person> original = new Stack<Person>();
        original.Push(complexObj1);
        original.Push(complexObj2);

        // Act
        Stack<Person>? cloned = FastCloner.DeepClone(original);

        // Modify original objects
        complexObj1.Name = "Alice Modified";
        complexObj2.Age = 26;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");

            Person topCloned = cloned.Pop();
            Person bottomCloned = cloned.Pop();

            await Assert.That(topCloned).IsNotSameReferenceAs(complexObj2).Because("Should create new object instances");
            await Assert.That(bottomCloned).IsNotSameReferenceAs(complexObj1).Because("Should create new object instances");

            await Assert.That(topCloned.Name).IsEqualTo("Bob").Because("Cloned objects should not reflect changes to original");
            await Assert.That(topCloned.Age).IsEqualTo(25).Because("Cloned objects should not reflect changes to original");
            await Assert.That(bottomCloned.Name).IsEqualTo("Alice").Because("Cloned objects should not reflect changes to original");
            await Assert.That(bottomCloned.Age).IsEqualTo(30).Because("Cloned objects should not reflect changes to original");

            // Assert
        }
    }

    [Test]
    public async Task ImmutableList_DeepClone_ShouldCreateNewInstance()
    {
        // Arrange
        ImmutableList<string> original = ImmutableList.Create("One", "Two", "Three");

        // Act
        ImmutableList<string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<ImmutableList<string>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            // Verify elements and order
            await Assert.That(cloned[0]).IsEqualTo("One").Because("First element should be preserved");
            await Assert.That(cloned[1]).IsEqualTo("Two").Because("Second element should be preserved");
            await Assert.That(cloned[2]).IsEqualTo("Three").Because("Third element should be preserved");

            // Verify immutability behavior
            ImmutableList<string> newList = cloned.Add("Four");
            await Assert.That(cloned.Count).IsEqualTo(3).Because("Original cloned list should remain unchanged after add");
            await Assert.That(newList.Count).IsEqualTo(4).Because("New list should contain added element");
            await Assert.That(newList[3]).IsEqualTo("Four").Because("New list should have correct added element");

            // Assert
        }
    }

    [Test]
    public async Task ImmutableHashSet_DeepClone_ShouldPreserveSetOperations()
    {
        // Arrange
        ImmutableHashSet<int> original = ImmutableHashSet.Create(1, 2, 3, 4, 5);

        // Act
        ImmutableHashSet<int>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<ImmutableHashSet<int>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            // Verify elements
            foreach (int item in original)
            {
                await Assert.That(cloned.Contains(item)).IsTrue().Because($"Cloned set should contain {item}");
            }

            // Verify set operations work correctly
            ImmutableHashSet<int> otherSet = ImmutableHashSet.Create(4, 5, 6, 7);

            ImmutableHashSet<int> intersection = cloned.Intersect(otherSet);
            await Assert.That(intersection.Count).IsEqualTo(2).Because("Intersection should have correct count");
            await Assert.That(intersection.Contains(4)).IsTrue().Because("Intersection should contain common elements");
            await Assert.That(intersection.Contains(5)).IsTrue().Because("Intersection should contain common elements");

            ImmutableHashSet<int> union = cloned.Union(otherSet);
            await Assert.That(union.Count).IsEqualTo(7).Because("Union should have correct count");
            for (int i = 1; i <= 7; i++)
            {
                await Assert.That(union.Contains(i)).IsTrue().Because($"Union should contain {i}");
            }

            ImmutableHashSet<int> except = cloned.Except(otherSet);
            await Assert.That(except.Count).IsEqualTo(3).Because("Except should have correct count");
            await Assert.That(except.Contains(1)).IsTrue().Because("Except should contain non-common elements");
            await Assert.That(except.Contains(2)).IsTrue().Because("Except should contain non-common elements");
            await Assert.That(except.Contains(3)).IsTrue().Because("Except should contain non-common elements");

            // Verify immutability behavior
            ImmutableHashSet<int> newSet = cloned.Add(6);
            await Assert.That(cloned.Count).IsEqualTo(5).Because("Original cloned set should remain unchanged after add");
            await Assert.That(newSet.Count).IsEqualTo(6).Because("New set should contain added element");
            await Assert.That(newSet.Contains(6)).IsTrue().Because("New set should have correct added element");

            // Assert
        }
    }

    class EventPropertyNotifyChangedCls
    {
        [FastClonerIgnore]
        public event PropertyChangedEventHandler? PropertyChanged = (_, _) => { };

        public List<int> TestList { get; set; } = [1, 2, 3];

        public bool HasPropertyChangedSubscribers()
        {
            return PropertyChanged != null;
        }
    }

    struct ClonerIgnoreStructTest
    {
        [FastClonerIgnore]
        public int MyInt;
    }

    struct ClonerIgnoreStructTestNullable
    {
        [FastClonerIgnore]
        public int? MyInt;
    }

    public class MyJsonNodeClass
    {
        public string Name { get; set; }
        public JsonNode? Config { get; set; }
    }

    [Test]
    public async Task CloneJsonNode()
    {
        // Arrange
        MyJsonNodeClass original = new MyJsonNodeClass
        {
            Name = "Test",
            Config = new JsonObject
            {
                ["a"] = 1
            }
        };

        // Act
        MyJsonNodeClass clone = original.DeepClone();
        ((JsonObject)clone.Config!)["a"] = 999;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original).Because("Clone should be a different instance");
            await Assert.That(clone.Name).IsEqualTo(original.Name).Because("Name should be copied");
            await Assert.That(clone.Config).IsNotSameReferenceAs(original.Config).Because("Config should be a different instance");
            await Assert.That(((JsonObject)original.Config!)["a"]!.GetValue<int>()).IsEqualTo(1).Because("Original config should remain unchanged");

            // Assert
        }
    }
    
    public class DictionaryWithNonOptionalCtor : Dictionary<string, string>
    {
        public int RequiredValue { get; }
        
        public DictionaryWithNonOptionalCtor(int requiredValue)
        {
            RequiredValue = requiredValue;
        }
    }

    [Test]
    public async Task DictionaryWithNonOptionalConstructor_ShouldFallbackToMemberwiseClone()
    {
        // Arrange
        DictionaryWithNonOptionalCtor original = new DictionaryWithNonOptionalCtor(42)
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        // Act
        DictionaryWithNonOptionalCtor clone = original.DeepClone();
        clone["key1"] = "value3";
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(clone).IsAssignableTo<DictionaryWithNonOptionalCtor>().Because("Should preserve type");
            await Assert.That(clone.Count).IsEqualTo(original.Count).Because("Should preserve count");
            await Assert.That(clone["key1"]).IsEqualTo("value3").Because("Should preserve values");
            await Assert.That(clone["key2"]).IsEqualTo("value2").Because("Should preserve values");
            await Assert.That(original["key1"]).IsEqualTo("value1").Because("Should not affect original value");

            // Assert
        }
    }

    [Test]
    public async Task CloneAllJsonNodeTypes()
    {
        // Test JsonObject
        JsonObject originalObject = new JsonObject
        {
            ["string"] = "test",
            ["number"] = 42,
            ["boolean"] = true,
            ["null"] = null
        };

        JsonNode clonedObject = originalObject.DeepClone();
        ((JsonObject)clonedObject)["string"] = "modified";

        using (Assert.Multiple())
        {
            await Assert.That(clonedObject).IsNotSameReferenceAs(originalObject).Because("JsonObject should be deep cloned");
            await Assert.That(((JsonObject)originalObject)["string"]!.GetValue<string>()).IsEqualTo("test").Because("Original JsonObject should remain unchanged");
            await Assert.That(((JsonObject)clonedObject)["string"]!.GetValue<string>()).IsEqualTo("modified").Because("Cloned JsonObject should be modified");

        }

        // Test JsonArray
        JsonArray originalArray = ["item1", "item2", "item3"];
        JsonNode clonedArray = originalArray.DeepClone();
        clonedArray[0] = "modified";

        using (Assert.Multiple())
        {
            await Assert.That(clonedArray).IsNotSameReferenceAs(originalArray).Because("JsonArray should be deep cloned");
            await Assert.That(originalArray[0]!.GetValue<string>()).IsEqualTo("item1").Because("Original JsonArray should remain unchanged");
            await Assert.That(clonedArray[0]!.GetValue<string>()).IsEqualTo("modified").Because("Cloned JsonArray should be modified");

        }

        // Test JsonValue
        JsonValue originalValue = JsonValue.Create("test value");
        JsonNode clonedValue = originalValue.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(clonedValue).IsNotSameReferenceAs(originalValue).Because("JsonValue should be deep cloned");
            await Assert.That(originalValue!.GetValue<string>()).IsEqualTo("test value").Because("Original JsonValue should remain unchanged");
            await Assert.That(clonedValue!.GetValue<string>()).IsEqualTo("test value").Because("Cloned JsonValue should have same value");

        }

        // Test nested structure
        JsonObject nestedOriginal = new JsonObject
        {
            ["array"] = new JsonArray { 1, 2, 3 },
            ["object"] = new JsonObject { ["nested"] = "value" }
        };

        JsonNode nestedClone = nestedOriginal.DeepClone();
        ((JsonArray)nestedClone["array"]!)[0] = 999;
        ((JsonObject)nestedClone["object"]!)["nested"] = "modified";

        using (Assert.Multiple())
        {
            await Assert.That(nestedClone).IsNotSameReferenceAs(nestedOriginal).Because("Nested JsonNode should be deep cloned");
            await Assert.That(((JsonArray)nestedOriginal["array"]!)[0]!.GetValue<int>()).IsEqualTo(1).Because("Original nested array should remain unchanged");
            await Assert.That(((JsonArray)nestedClone["array"]!)[0]!.GetValue<int>()).IsEqualTo(999).Because("Cloned nested array should be modified");
            await Assert.That(((JsonObject)nestedOriginal["object"]!)["nested"]!.GetValue<string>()).IsEqualTo("value").Because("Original nested object should remain unchanged");
            await Assert.That(((JsonObject)nestedClone["object"]!)["nested"]!.GetValue<string>()).IsEqualTo("modified").Because("Cloned nested object should be modified");

        }
    }

    [Test]
    public async Task JsonNodeReflectionCaching_ShouldCacheProcessors()
    {
        JsonObject original = new JsonObject { ["test"] = "value" };

        // First clone - should generate and cache the processor
        JsonNode clone1 = original.DeepClone();

        // Second clone - should use the cached processor
        JsonNode clone2 = original.DeepClone();

        // Third clone - should also use the cached processor
        JsonNode clone3 = original.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(clone1).IsNotSameReferenceAs(original).Because("First clone should be different instance");
            await Assert.That(clone2).IsNotSameReferenceAs(original).Because("Second clone should be different instance");
            await Assert.That(clone3).IsNotSameReferenceAs(original).Because("Third clone should be different instance");
            await Assert.That(clone1).IsNotSameReferenceAs(clone2).Because("Clones should be different from each other");
            await Assert.That(clone2).IsNotSameReferenceAs(clone3).Because("Clones should be different from each other");

        }
    }

    [Test]
    public async Task JsonNodeFullNameIsNull()
    {
        JsonNode node = new JsonObject { ["test"] = "value" };

        using (Assert.Multiple())
        {
            await Assert.That(node.GetType().FullName).IsEqualTo("System.Text.Json.Nodes.JsonObject").Because("JsonObject has a FullName");
            await Assert.That(FastClonerSafeTypes.CanReturnSameObject(node.GetType())).IsFalse().Because("JsonObject should not be considered a safe type");

            Type jsonNodeType = typeof(JsonNode);
            await Assert.That(jsonNodeType.FullName).IsEqualTo("System.Text.Json.Nodes.JsonNode").Because("JsonNode has a FullName");
            await Assert.That(FastClonerSafeTypes.CanReturnSameObject(jsonNodeType)).IsFalse().Because("JsonNode should not be considered a safe type");

        }
    }

    [Test]
    public async Task CloneSimpleInt()
    {
        int i = 42.DeepClone();
        await Assert.That(i).IsEqualTo(42);
    }

    [Test]
    public async Task StructMembersIgnoreNullable()
    {
        // Arrange
        ClonerIgnoreStructTestNullable inst = new ClonerIgnoreStructTestNullable
        {
            MyInt = 49
        };

        // Act
        ClonerIgnoreStructTestNullable cloned = inst.DeepClone();

        // Assert
        await Assert.That(cloned.MyInt).IsNull().Because("Should ignore the field");
    }

    [Test]
    public async Task StructMembersIgnore()
    {
        // Arrange
        ClonerIgnoreStructTest inst = new ClonerIgnoreStructTest
        {
            MyInt = 49
        };

        // Act
        ClonerIgnoreStructTest cloned = inst.DeepClone();

        // Assert
        await Assert.That(cloned.MyInt).IsEqualTo(0).Because("Should ignore the field");
    }

    [Test]
    public async Task EventPropertyNotifyChangedIgnore()
    {
        // Arrange
        EventPropertyNotifyChangedCls cls = new EventPropertyNotifyChangedCls();

        // Act
        EventPropertyNotifyChangedCls cloned = cls.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(cls).Because("Should create new instance");
            await Assert.That(cls.HasPropertyChangedSubscribers()).IsTrue().Because("Original should have event subscribers");
            await Assert.That(cloned.HasPropertyChangedSubscribers()).IsFalse().Because("Ignored event should be null after cloning");
            await Assert.That(cloned.TestList).IsNotSameReferenceAs(cls.TestList).Because("TestList should be deep cloned");
            await Assert.That(cloned.TestList).IsEquivalentTo(cls.TestList).Because("TestList content should be preserved");

            // Assert
        }
    }

    [Test]
    public async Task ImmutableDictionary_DeepClone_WithComplexObjects_ShouldCreateDeepCopy()
    {
        // Arrange
        Person complexObj1 = new Person { Name = "Alice", Age = 30 };
        Person complexObj2 = new Person { Name = "Bob", Age = 25 };

        ImmutableDictionary<string, Person> original = ImmutableDictionary.CreateRange(new Dictionary<string, Person>
        {
            ["person1"] = complexObj1,
            ["person2"] = complexObj2
        });

        // Act
        ImmutableDictionary<string, Person>? cloned = FastCloner.DeepClone(original);

        // Modify original objects
        complexObj1.Name = "Alice Modified";
        complexObj2.Age = 26;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<ImmutableDictionary<string, Person>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            // Verify keys are preserved
            await Assert.That(cloned.ContainsKey("person1")).IsTrue().Because("Should contain original keys");
            await Assert.That(cloned.ContainsKey("person2")).IsTrue().Because("Should contain original keys");

            // Verify key lookup works correctly
            bool person1Found = cloned.TryGetValue("person1", out Person person1Value);
            bool person2Found = cloned.TryGetValue("person2", out Person person2Value);

            await Assert.That(person1Found).IsTrue().Because("Should be able to retrieve value by key");
            await Assert.That(person2Found).IsTrue().Because("Should be able to retrieve value by key");
            await Assert.That(person1Value.Name).IsEqualTo("Alice").Because("Retrieved value should have correct properties");
            await Assert.That(person2Value.Name).IsEqualTo("Bob").Because("Retrieved value should have correct properties");

            Person newPerson = new Person { Name = "Charlie", Age = 35 };
            ImmutableDictionary<string, Person> newDict = cloned.Add("person3", newPerson);
            await Assert.That(cloned.Count).IsEqualTo(2).Because("Original cloned dictionary should remain unchanged after add");
            await Assert.That(newDict.Count).IsEqualTo(3).Because("New dictionary should contain added element");
            await Assert.That(newDict["person3"].Name).IsEqualTo("Charlie").Because("New dictionary should have correct added element");

            // Assert
        }
    }

    [Test]
    public async Task ConcurrentStack_DeepClone_ShouldCreateNewInstance()
    {
        // Arrange
        ConcurrentStack<string> original = new ConcurrentStack<string>();
        original.Push("One");
        original.Push("Two");
        original.Push("Three");

        // Act
        ConcurrentStack<string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<ConcurrentStack<string>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            // Verify stack order by popping elements
            string[] clonedItems = new string[3];
            bool success = cloned.TryPopRange(clonedItems, 0, 3) == 3;
            await Assert.That(success).IsTrue().Because("Should be able to pop all elements");

            await Assert.That(clonedItems[0]).IsEqualTo("Three").Because("Top element should be preserved");
            await Assert.That(clonedItems[1]).IsEqualTo("Two").Because("Second element should be preserved");
            await Assert.That(clonedItems[2]).IsEqualTo("One").Because("Bottom element should be preserved");
            await Assert.That(cloned.Count).IsEqualTo(0).Because("Should be empty after popping all elements");

            // Verify original stack is unchanged
            await Assert.That(original.Count).IsEqualTo(3).Because("Original stack should remain unchanged");

            // Assert
        }
    }

    [Test]
    public async Task ConcurrentQueue_DeepClone_WithComplexObjects_ShouldCreateDeepCopy()
    {
        // Arrange
        Person complexObj1 = new Person { Name = "Eve", Age = 32 };
        Person complexObj2 = new Person { Name = "Frank", Age = 27 };

        ConcurrentQueue<Person> original = new ConcurrentQueue<Person>();
        original.Enqueue(complexObj1);
        original.Enqueue(complexObj2);

        // Act
        ConcurrentQueue<Person>? cloned = FastCloner.DeepClone(original);

        // Modify original objects
        complexObj1.Name = "Eve Modified";
        complexObj2.Age = 28;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<ConcurrentQueue<Person>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            Person firstCloned, secondCloned;
            bool firstSuccess = cloned.TryDequeue(out firstCloned);
            bool secondSuccess = cloned.TryDequeue(out secondCloned);

            await Assert.That(firstSuccess).IsTrue().Because("Should be able to dequeue first element");
            await Assert.That(secondSuccess).IsTrue().Because("Should be able to dequeue second element");

            await Assert.That(firstCloned).IsNotSameReferenceAs(complexObj1).Because("Should create new object instances");
            await Assert.That(secondCloned).IsNotSameReferenceAs(complexObj2).Because("Should create new object instances");

            await Assert.That(firstCloned.Name).IsEqualTo("Eve").Because("Cloned objects should not reflect changes to original");
            await Assert.That(firstCloned.Age).IsEqualTo(32).Because("Cloned objects should not reflect changes to original");
            await Assert.That(secondCloned.Name).IsEqualTo("Frank").Because("Cloned objects should not reflect changes to original");
            await Assert.That(secondCloned.Age).IsEqualTo(27).Because("Cloned objects should not reflect changes to original");

            // Verify original queue is unchanged
            await Assert.That(original.Count).IsEqualTo(2).Because("Original queue should remain unchanged");

            // Assert
        }
    }


    [Test]
    public async Task Queue_DeepClone_ShouldCreateNewInstance()
    {
        // Arrange
        Queue<string> original = new Queue<string>();
        original.Enqueue("One");
        original.Enqueue("Two");
        original.Enqueue("Three");

        // Act
        Queue<string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned).IsAssignableTo<Queue<string>>().Because("Should preserve type");
            await Assert.That(cloned.Count).IsEqualTo(original.Count).Because("Should have same count");

            // Verify queue order by dequeuing elements
            await Assert.That(cloned.Dequeue()).IsEqualTo("One").Because("First element should be preserved");
            await Assert.That(cloned.Dequeue()).IsEqualTo("Two").Because("Second element should be preserved");
            await Assert.That(cloned.Dequeue()).IsEqualTo("Three").Because("Last element should be preserved");
            await Assert.That(cloned.Count).IsEqualTo(0).Because("Should be empty after dequeuing all elements");

            // Assert
        }
    }

    [Test]
    public async Task Queue_DeepClone_WithComplexObjects_ShouldCreateDeepCopy()
    {
        // Arrange
        Person complexObj1 = new Person { Name = "Charlie", Age = 35 };
        Person complexObj2 = new Person { Name = "Diana", Age = 28 };

        Queue<Person> original = new Queue<Person>();
        original.Enqueue(complexObj1);
        original.Enqueue(complexObj2);

        // Act
        Queue<Person>? cloned = FastCloner.DeepClone(original);

        // Modify original objects
        complexObj1.Name = "Charlie Modified";
        complexObj2.Age = 29;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");

            Person firstCloned = cloned.Dequeue();
            Person secondCloned = cloned.Dequeue();

            await Assert.That(firstCloned).IsNotSameReferenceAs(complexObj1).Because("Should create new object instances");
            await Assert.That(secondCloned).IsNotSameReferenceAs(complexObj2).Because("Should create new object instances");

            await Assert.That(firstCloned.Name).IsEqualTo("Charlie").Because("Cloned objects should not reflect changes to original");
            await Assert.That(firstCloned.Age).IsEqualTo(35).Because("Cloned objects should not reflect changes to original");
            await Assert.That(secondCloned.Name).IsEqualTo("Diana").Because("Cloned objects should not reflect changes to original");
            await Assert.That(secondCloned.Age).IsEqualTo(28).Because("Cloned objects should not reflect changes to original");

            // Assert
        }
    }

    [Test]
    public async Task Stack_DeepClone_EmptyStack_ShouldCreateEmptyClone()
    {
        // Arrange
        Stack<int> original = new Stack<int>();

        // Act
        Stack<int>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned.Count).IsEqualTo(0).Because("Cloned stack should be empty");

            // Assert
        }
    }

    [Test]
    public async Task Queue_DeepClone_EmptyQueue_ShouldCreateEmptyClone()
    {
        // Arrange
        Queue<int> original = new Queue<int>();

        // Act
        Queue<int>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned.Count).IsEqualTo(0).Because("Cloned queue should be empty");

            // Assert
        }
    }

    // Helper class for complex object tests
    private class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class ReadOnlySet<T> : IReadOnlySet<T>
    {
        private readonly ISet<T> set;

        public ReadOnlySet(ISet<T> set)
        {
            this.set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public int Count => set.Count;
        public bool Contains(T item) => set.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => set.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => set.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => set.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    [Test]
    public async Task ReadOnlyDictionary_WithComplexValues_Clone_ShouldDeepClone()
    {
        // Arrange
        Dictionary<string, List<string>> originalDict = new Dictionary<string, List<string>>
        {
            ["List1"] = ["A", "B"],
            ["List2"] = ["C", "D"]
        };
        ReadOnlyDictionary<string, List<string>> original = new ReadOnlyDictionary<string, List<string>>(originalDict);

        // Act
        ReadOnlyDictionary<string, List<string>>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned["List1"]).IsNotSameReferenceAs(original["List1"]).Because("Should deep clone values");
            await Assert.That(cloned["List2"]).IsNotSameReferenceAs(original["List2"]).Because("Should deep clone values");
            await Assert.That(cloned["List1"]).IsEquivalentTo(original["List1"]).Because("Should preserve value contents");
            await Assert.That(cloned["List2"]).IsEquivalentTo(original["List2"]).Because("Should preserve value contents");

            // Assert
        }
    }

    [Test]
    public async Task ReadOnlyDictionary_WithNullValues_Clone_ShouldPreserveNulls()
    {
        // Arrange
        Dictionary<string, string> originalDict = new Dictionary<string, string>
        {
            ["NotNull"] = "Value",
            ["Null"] = null
        };
        ReadOnlyDictionary<string, string> original = new ReadOnlyDictionary<string, string>(originalDict);

        // Act
        ReadOnlyDictionary<string, string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned["NotNull"]).IsEqualTo("Value").Because("Should preserve non-null values");
            await Assert.That(cloned["Null"]).IsNull().Because("Should preserve null values");

            // Assert
        }
    }

    [Test]
    public async Task ReadOnlyDictionary_Empty_Clone_ShouldCreateEmptyInstance()
    {
        // Arrange
        ReadOnlyDictionary<string, int> original = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

        // Act
        ReadOnlyDictionary<string, int>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(cloned.Count).IsEqualTo(0).Because("Should be empty");

            // Assert
        }
    }

    [Test]
    public async Task ReadOnlyDictionary_WithKeyValuePairs_Clone_ShouldPreserveEnumeration()
    {
        // Arrange
        Dictionary<int, string> originalDict = new Dictionary<int, string> { [1] = "One", [2] = "Two" };
        ReadOnlyDictionary<int, string> original = new ReadOnlyDictionary<int, string>(originalDict);

        // Act
        ReadOnlyDictionary<int, string>? cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned.Keys).IsEquivalentTo(original.Keys).Because("Should preserve keys");
            await Assert.That(cloned.Values).IsEquivalentTo(original.Values).Because("Should preserve values");
            await Assert.That(cloned).IsEquivalentTo(original).Because("Should preserve key-value pairs");

            // Assert
        }
    }

    [Test]
    public async Task ExpandoObject_With_Circular_Reference_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        dynamic nested = new ExpandoObject();
        original.Name = "Original";
        original.Nested = nested;
        nested.Parent = original; // Circular reference

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object?)cloned).IsNotSameReferenceAs((object?)original).Because("Cloned object should be a new instance");
            await Assert.That((object?)cloned.Nested).IsNotSameReferenceAs((object?)original.Nested).Because("Nested object should be cloned");
            await Assert.That((string)cloned.Name).IsEqualTo("Original").Because("Properties should be copied");
            await Assert.That((object?)cloned.Nested.Parent).IsSameReferenceAs((object?)cloned).Because("Circular reference should point to cloned instance");

            // Assert
        }
    }

    [Test]
    public async Task Mixed_Dynamic_And_Static_Types_Clone()
    {
        // Arrange
        StaticType staticObject = new StaticType { Value = "Static" };
        dynamic dynamic = new ExpandoObject();
        dynamic.Static = staticObject;
        dynamic.Name = "Dynamic";

        // Act
        dynamic cloned = FastCloner.DeepClone(dynamic);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object?)cloned.Static).IsNotSameReferenceAs((object?)staticObject).Because("Static type should be cloned");
            await Assert.That((string)cloned.Static.Value).IsEqualTo("Static").Because("Static type properties should be copied");
            await Assert.That((string)cloned.Name).IsEqualTo("Dynamic").Because("Dynamic properties should be copied");

            // Assert
        }
    }

    private class StaticType
    {
        public string Value { get; set; }
    }

    [Test]
    public async Task ExpandoObject_With_Null_Values_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.NullProperty = null;
        original.ValidProperty = "NotNull";

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(((object)cloned.NullProperty)).IsNull().Because("Null properties should remain null");
            await Assert.That((string)cloned.ValidProperty).IsEqualTo("NotNull").Because("Non-null properties should be copied");

            // Assert
        }
    }

    [Test]
    public async Task Dynamic_Object_With_Complex_Types_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.DateTime = DateTime.Now;
        original.Guid = Guid.NewGuid();
        original.TimeSpan = TimeSpan.FromHours(1);

        // Act
        dynamic cloned = FastCloner.DeepClone(original);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((DateTime)cloned.DateTime).IsEqualTo((DateTime)original.DateTime).Because("DateTime should be copied");
            await Assert.That((Guid)cloned.Guid).IsEqualTo((Guid)original.Guid).Because("Guid should be copied");
            await Assert.That((TimeSpan)cloned.TimeSpan).IsEqualTo((TimeSpan)original.TimeSpan).Because("TimeSpan should be copied");

            // Assert
        }
    }


    private class Node
    {
        public string Name { get; set; }
        public Node Next { get; set; }
    }

    [Test]
    public async Task Test_ExpressionTree_OrderBy2()
    {
        IEnumerable<Tuple<int, string>> l = new List<int> { 2, 1, 3, 4, 5 }.Select(y => new Tuple<int, string>(y, y.ToString(CultureInfo.InvariantCulture)));
        IOrderedQueryable<Tuple<int, string>> q = l.AsQueryable().OrderBy(x => x.Item1);
        IOrderedQueryable<Tuple<int, string>> q2 = q.DeepClone();
        await Assert.That(q2.ToArray()[0].Item1).IsEqualTo(1);
        await Assert.That(q.ToArray().Length).IsEqualTo(5);
    }

    [Test]
    [Property("Description", "Tests works on local SQL Server with AdventureWorks database")]
    [Skip("Test on MS Server")]
    public async Task Clone_EfQuery1()
    {
        AdventureContext at = new AdventureContext();
        // var at2 = at.DeepClone();
        // Console.WriteLine(at.ChangeTracker);
        // Console.WriteLine(at.ChangeTracker);
        IQueryable<Currency> q = at.Currencies.Where(x => x.CurrencyCode == "AUD");
        IQueryable<Currency> q2 = q.DeepClone();

        // var q2 = q.DeepClone();
        // Console.WriteLine(q2.);
        // Assert.That(q.ToArray().Length, Is.EqualTo(1));
        await Assert.That(q2.ToArray().Length).IsEqualTo(1);
    }

    [Test]
    [Property("Description", "Tests works on local SQL Server with AdventureWorks database")]
    [Skip("Test on MS Server")]
    public async Task Clone_EfQuery2()
    {
        IOrderedQueryable<Currency> q = new AdventureContext().Currencies.OrderBy(x => x.Name);
        IOrderedQueryable<Currency> q2 = q.DeepClone();
        int cnt = q.Count();
        await Assert.That(q2.Count()).IsEqualTo(cnt);
    }



    [Test]
    public async Task FontCloningTest()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Arrange
        Font originalFont = new Font("Arial", 12, FontStyle.Bold);

        // Act
        Font clonedFont = originalFont.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clonedFont).IsNotNull();
            await Assert.That(clonedFont.Name).IsEqualTo(originalFont.Name);
            await Assert.That(clonedFont.Size).IsEqualTo(originalFont.Size);
            await Assert.That(clonedFont.Style).IsEqualTo(originalFont.Style);
            await Assert.That(clonedFont.Unit).IsEqualTo(originalFont.Unit);
            await Assert.That(clonedFont.GdiCharSet).IsEqualTo(originalFont.GdiCharSet);
            await Assert.That(clonedFont.GdiVerticalFont).IsEqualTo(originalFont.GdiVerticalFont);

            // Ensure the cloned font is a different instance
            await Assert.That(ReferenceEquals(originalFont, clonedFont)).IsFalse();

            // Assert
        }

    }


    [Test]
    public async Task Lazy_Clone()
    {
        LazyClass lazy = new LazyClass();
        LazyClass clone = lazy.DeepClone();
        int v = LazyClass.Counter;
        await Assert.That(clone.GetValue()).IsEqualTo((v + 1).ToString(CultureInfo.InvariantCulture));
        await Assert.That(lazy.GetValue()).IsEqualTo((v + 2).ToString(CultureInfo.InvariantCulture));
    }

    public class LazyClass
    {
        public static int Counter;

        private readonly LazyRef<object> lazyValue = new LazyRef<object>(() => (++Counter).ToString(CultureInfo.InvariantCulture));

        public string GetValue() => lazyValue.Value.ToString();
    }

    [Table("Currency", Schema = "Sales")]
    public class Currency
    {
        [Key]
        public string CurrencyCode { get; set; }

        [Column]
        public string Name { get; set; }
    }

    public class AdventureContext : DbContext
    {
        public AdventureContext()
        {
        }

        public DbSet<Currency> Currencies { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer(@"Server=.;Database=AdventureWorks;Trusted_Connection=true;MultipleActiveResultSets=true;Encrypt=False");
    }

    [Test]
    public void GenericComparer_Clone()
    {
        TestComparer comparer = new TestComparer();
        comparer.DeepClone();
    }

    [Test]
    public async Task Closure_Clone()
    {
        int a = 0;
        Func<int> f = () => ++a;
        Func<int> fCopy = f.DeepClone();
        // delegates are shallow-copied, so both share the same closure
        await Assert.That(f()).IsEqualTo(1);
        await Assert.That(fCopy()).IsEqualTo(2);
        await Assert.That(a).IsEqualTo(2);
    }

    private class TestComparer : Comparer<int>
    {
        // make object unsafe to work
        private object fieldX = new object();

        public override int Compare(int x, int y) => x.CompareTo(y);
    }

    public sealed class LazyRef<T>
    {
        private Func<T> initializer;
        private T value;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public T Value
        {
            get
            {
                if (initializer != null)
                {
                    value = initializer();
                    initializer = null;
                }

                return value;
            }
            set
            {
                this.value = value;
                initializer = null;
            }
        }

        public LazyRef(Func<T> initializer) => this.initializer = initializer;
    }

    [Test]
    public async Task CanCopyInterfaceField()
    {
        MyObject o = new MyObject();

        MyIClass original = new MyIClass
        {
            Field1 = o,
            Field2 = o
        };

        MyIClass result = original.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(original.Field1).IsSameReferenceAs(original.Field2).Because("Original objects should be same");
            await Assert.That(result.Field1).IsSameReferenceAs(result.Field2).Because("Cloned objects should be same");

        }
    }

    public class MyIClass
    {
        public IMyInterface1 Field1;
        public IMyInterface2 Field2;
    }

    public interface IMyInterface1
    {
    }

    public interface IMyInterface2
    {
    }

    public class MyObject : IMyInterface1, IMyInterface2
    {
    }

    [Test]
    public async Task JsonObjectConstructorTest()
    {
        // This test verifies that our FindCallableConstructor fix works
        // JsonObject has constructor: JsonObject(JsonNodeOptions? options = null)
        // So it should be callable with no arguments

        JsonObject original = new JsonObject { ["test"] = "value" };

        // This should now work without the special JsonNode processors
        JsonNode clone = original.DeepClone();

        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(clone).IsAssignableTo<JsonObject>().Because("Should preserve type");
            await Assert.That(((JsonObject)clone)["test"]!.GetValue<string>()).IsEqualTo("value").Because("Should preserve content");

        }
    }

    public class MyNonGenericDict : IDictionary<string, int>
    {
        private readonly Dictionary<string, int> _innerDict;
        private readonly int _defaultValue;

        public MyNonGenericDict(int defaultValue = 0)
        {
            _defaultValue = defaultValue;
            _innerDict = new Dictionary<string, int>();
        }

        public int this[string key]
        {
            get => _innerDict.GetValueOrDefault(key, _defaultValue);
            set => _innerDict[key] = value;
        }

        public ICollection<string> Keys => _innerDict.Keys;
        public ICollection<int> Values => _innerDict.Values;
        public int Count => _innerDict.Count;
        public bool IsReadOnly => false;

        public void Add(string key, int value) => _innerDict.Add(key, value);
        public void Add(KeyValuePair<string, int> item) => _innerDict.Add(item.Key, item.Value);
        public void Clear() => _innerDict.Clear();
        public bool Contains(KeyValuePair<string, int> item) => _innerDict.Contains(item);
        public bool ContainsKey(string key) => _innerDict.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, int>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, int>>)_innerDict).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, int>> GetEnumerator() => _innerDict.GetEnumerator();
        public bool Remove(string key) => _innerDict.Remove(key);
        public bool Remove(KeyValuePair<string, int> item) => _innerDict.Remove(item.Key);
        public bool TryGetValue(string key, out int value) => _innerDict.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    public async Task NonGenericDictionaryWithOptionalConstructor_ShouldDeepClone()
    {
        // Arrange
        MyNonGenericDict original = new MyNonGenericDict(defaultValue: 42)
        {
            ["key1"] = 100,
            ["key2"] = 200,
            ["key3"] = 300
        };

        // Act
        MyNonGenericDict clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original).Because("Should create new instance");
            await Assert.That(clone).IsTypeOf<MyNonGenericDict>().Because("Should preserve type");
            await Assert.That(clone.Count).IsEqualTo(original.Count).Because("Should have same count");
            await Assert.That(clone["key1"]).IsEqualTo(100).Because("Should preserve first value");
            await Assert.That(clone["key2"]).IsEqualTo(200).Because("Should preserve second value");
            await Assert.That(clone["key3"]).IsEqualTo(300).Because("Should preserve third value");

            clone["key1"] = 999;
            await Assert.That(original["key1"]).IsEqualTo(100).Because("Original should remain unchanged");
            await Assert.That(clone["key1"]).IsEqualTo(999).Because("Clone should reflect changes");

            // Assert
        }
    }

    [Test]
    public async Task Drawing_Image_DeepClone_Test()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ;
            return;
        }
        
        // Arrange
        Image original = new Bitmap(10, 10);

        // Act
        Image clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original);
            await Assert.That(clone.Width).IsEqualTo(original.Width);
            await Assert.That(clone.Height).IsEqualTo(original.Height);

            // Assert
        }
    }

    [Test]
    public async Task Drawing_Icon_DeepClone_Test()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ;
            return;
        }
        
        // Arrange
        using Bitmap bitmap = new Bitmap(16, 16);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(Color.Red);
        IntPtr hIcon = bitmap.GetHicon();
        Icon original = Icon.FromHandle(hIcon);

        // Act
        Icon clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original);
            await Assert.That(clone.Width).IsEqualTo(original.Width);
            await Assert.That(clone.Height).IsEqualTo(original.Height);

            // Assert
        }
    }

    [Test]
    public async Task Drawing_Brush_DeepClone_Test()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ;
            return;
        }
        
        // Arrange
        SolidBrush original = new SolidBrush(Color.Red);

        // Act
        SolidBrush clone = original.DeepClone();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotSameReferenceAs(original);
            await Assert.That((clone).Color.R).IsEqualTo((original).Color.R);
            await Assert.That((clone).Color.G).IsEqualTo((original).Color.G);
            await Assert.That((clone).Color.B).IsEqualTo((original).Color.B);
            await Assert.That((clone).Color.A).IsEqualTo((original).Color.A);

            // Assert
        }

        original.Color = Color.Blue;
        await Assert.That(clone.Color.B).IsEqualTo((byte)0);
    }

    [Test]
    public async Task AssemblyName_DeepClone_Test()
    {
        // Arrange
        AssemblyName original = new AssemblyName
        {
            Name = "MyTestAssembly",
            Version = new Version(1, 2, 3, 4)
        };
        Version originalVersion = new Version(1, 2, 3, 4);

        // Act
        AssemblyName clone = original.DeepClone();
        original.Version = new Version(5, 6, 7, 8); // Modify the original

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(clone).IsNotNull();
            await Assert.That(clone).IsNotSameReferenceAs(original);
            await Assert.That(clone.Name).IsEqualTo("MyTestAssembly");
            await Assert.That(clone.Version).IsEqualTo(originalVersion);
            await Assert.That(clone.Version).IsNotEqualTo(original.Version);

            // Assert
        }
    }
    
    public class LargeNode
    {
        public LargeNode Parent { get; set; }
        public LargeNode Child { get; set; }
        public LargeNode Previous { get; set; }
        public LargeNode Next { get; set; }
        public List<int> Data { get; set; } = [];
    }
    
    [Test]
    public async Task LargeCircular_Test()
    {
        // Arrange
        LargeNode root = new LargeNode { Data = [0] };
        LargeNode current = root;
        const int nodeCount = 50_000; // stackoverflow for n >= 8000

        for (int i = 1; i < nodeCount; i++)
        {
            LargeNode next = new LargeNode { Data = [i], Parent = current };
            current.Child = next;
            next.Previous = current;
            current.Next = next;
            current = next;
        }
        
        current.Next = root;
        root.Previous = current;

        // Act
        LargeNode clone = root.DeepClone();

        // Assert
        await Assert.That(clone).IsNotSameReferenceAs(root);

        LargeNode forward = clone;
        for (int i = 0; i < nodeCount; i++)
        {
            forward = forward.Next;
        }
        await Assert.That(forward).IsSameReferenceAs(clone);

        LargeNode backward = clone;
        for (int i = 0; i < nodeCount; i++)
        {
            backward = backward.Previous;
        }
        await Assert.That(backward).IsSameReferenceAs(clone);

        // Traverse to a deeply nested node
        LargeNode originalNode = root;
        LargeNode clonedNode = clone;
        for (int i = 0; i < nodeCount / 2; i++)
        {
            originalNode = originalNode.Next;
            clonedNode = clonedNode.Next;
        }
        
        await Assert.That(clonedNode).IsNotSameReferenceAs(originalNode);
        await Assert.That(clonedNode.Data).IsNotSameReferenceAs(originalNode.Data);
        await Assert.That(clonedNode.Data).IsEquivalentTo(originalNode.Data);

        clonedNode.Data.Add(999);
        await Assert.That(originalNode.Data).Count().IsEqualTo(1);
        await Assert.That(clonedNode.Data).Count().IsEqualTo(2);
        await Assert.That(originalNode.Data[0]).IsNotEqualTo(clonedNode.Data[1]);
    }
    
    [Test]
    public async Task SelfReferenced_WithInitOnlyField_Test()
    {
    	SelfReferencedWithInitOnlyField original = new SelfReferencedWithInitOnlyField
    	{
    		WithReadOnlyField = new ClassWithReadOnlyField()
    	};
    
    	SelfReferencedWithInitOnlyField clone = original.DeepClone();
    	
    	await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.WithReadOnlyField).IsNotSameReferenceAs(original.WithReadOnlyField);
        await Assert.That(clone.WithReadOnlyField.ReadOnlyValue).IsEqualTo(original.WithReadOnlyField.ReadOnlyValue);
    }
    
    private class SelfReferencedWithInitOnlyField
    {
    	public SelfReferencedWithInitOnlyField? Predecessor { get; set; }
    
    	public ClassWithReadOnlyField WithReadOnlyField { get; set; }
    }
    
    [Test]
    public async Task SelfReferenced_WithInitOnlyValueTypeField_Test()
    {
        SelfReferencedWithInitOnlyValueTypeField original = new SelfReferencedWithInitOnlyValueTypeField
        {
            WithReadOnlyValueTypeField = new ClassWithReadOnlyValueField()
        };
    
        SelfReferencedWithInitOnlyValueTypeField clone = original.DeepClone();
    	
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.WithReadOnlyValueTypeField).IsNotSameReferenceAs(original.WithReadOnlyValueTypeField);
        await Assert.That(clone.WithReadOnlyValueTypeField.ReadOnlyValue).IsEqualTo(original.WithReadOnlyValueTypeField.ReadOnlyValue);
    }
    
    private class SelfReferencedWithInitOnlyValueTypeField
    {
        public SelfReferencedWithInitOnlyValueTypeField? Predecessor { get; set; }
        
        public ClassWithReadOnlyValueField WithReadOnlyValueTypeField { get; set; }
    }

    private class ClassWithReadOnlyValueField
    {
        private readonly decimal readOnlyField = 1m;
        public decimal ReadOnlyValue => readOnlyField;
    }
    
    [Test]
    public async Task SelfReferenced_WithWritableValueTypeField_Test()
    {
        SelfReferencedWithWritableValueTypeField original = new SelfReferencedWithWritableValueTypeField
        {
            WithWritableValueTypeField = new ClassWithWritableValueTypeField()
        };
    
        SelfReferencedWithWritableValueTypeField clone = original.DeepClone();
    	
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.WithWritableValueTypeField).IsNotSameReferenceAs(original.WithWritableValueTypeField);
        await Assert.That(clone.WithWritableValueTypeField.ReadOnlyValue).IsEqualTo(original.WithWritableValueTypeField.ReadOnlyValue);
    }
    
    private class SelfReferencedWithWritableValueTypeField
    {
        public SelfReferencedWithWritableValueTypeField? Predecessor { get; set; }
        
        public ClassWithWritableValueTypeField WithWritableValueTypeField { get; set; }
    }

    private class ClassWithWritableValueTypeField
    {
        private decimal readOnlyField = 1m;
        public decimal ReadOnlyValue => readOnlyField;
    }
    
    [Test]
    public async Task SelfReferenced_WithMultipleReadOnlyProperties_Test()
    {
        SelfReferencedWithMultipleReadOnlyProperties original = new SelfReferencedWithMultipleReadOnlyProperties
        {
            WithMultipleReadOnlyProperties = new ClassWithMultipleReadOnlyProperties()
        };
    
        SelfReferencedWithMultipleReadOnlyProperties clone = original.DeepClone();
    	
        await Assert.That(clone).IsNotSameReferenceAs(original);
        await Assert.That(clone.WithMultipleReadOnlyProperties).IsNotSameReferenceAs(original.WithMultipleReadOnlyProperties);
        await Assert.That(clone.WithMultipleReadOnlyProperties.Name).IsEqualTo(original.WithMultipleReadOnlyProperties.Name);
        await Assert.That(clone.WithMultipleReadOnlyProperties.Id).IsEqualTo(original.WithMultipleReadOnlyProperties.Id);
    }
    
    private class SelfReferencedWithMultipleReadOnlyProperties
    {
        public SelfReferencedWithMultipleReadOnlyProperties? Predecessor { get; set; }
    
        public ClassWithMultipleReadOnlyProperties WithMultipleReadOnlyProperties { get; set; }
    }

    private class ClassWithMultipleReadOnlyProperties
    {
        public int Id { get; } = 1;
        public string Name { get; } = "Test";
    }
    
    [Test]
    public async Task ValueTask_Should_Be_Safe()
    {
        ValueTask<int> original = new ValueTask<int>(42);
        ValueTask<int> clone = original.DeepClone();
        
        await Assert.That(clone.Result).IsEqualTo(42);
    }

    [Test]
    public async Task ValueTask_From_Task_Should_Be_Safe()
    {
        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
        tcs.SetResult(42);
        ValueTask<int> original = new ValueTask<int>(tcs.Task);
        
        ValueTask<int> clone = original.DeepClone();
        
        await Assert.That(clone.Result).IsEqualTo(42);
    }


    [Test]
    public async Task WeakReferenceGeneric_DeepClone_VerifySafety()
    {
        string target = "target";
        WeakReference<string> weak = new WeakReference<string>(target);
        
        WeakReference<string> clone = weak.DeepClone();
        
        // Assert it is the SAME object (Reference Copy)
        await Assert.That(clone).IsSameReferenceAs(weak);

        bool hasTarget = clone.TryGetTarget(out string? clonedTarget);
        
        Console.WriteLine($"Original target alive: {weak.TryGetTarget(out _)}");
        Console.WriteLine($"Clone target alive: {hasTarget}");
    }
}