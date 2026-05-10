using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using FastCloner.Code;

namespace FastCloner.Tests;
public class CopyToObjectTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public async Task InterfaceTest()
    {
        SampleInterfaceClsWithProp source = new SampleInterfaceClsWithProp
        {
            ActivityData = new SampleActivityDataWithProp
            {
                Data = new SampleActivityParsedData
                {
                    Steps = ["A", "B", "C"]
                }
            }
        };

        SampleInterfaceClsWithProp to = source.DeepClone();
        await Assert.That(ReferenceEquals(source.ActivityData, to.ActivityData)).IsEqualTo(false);
    }

    public class KeyClass
    {
        public string Value { get; set; }
    }

    [Test]
    public async Task DictionaryBrokenAfterCloningTest()
    {
        // Arrange
        Dictionary<KeyClass, string> originalDict = new Dictionary<KeyClass, string>();
        KeyClass key = new KeyClass { Value = "TestKey" };
        originalDict[key] = "TestValue";

        // Act
        Dictionary<KeyClass, string> clonedDict = originalDict.DeepClone();
        KeyClass clonedKey = clonedDict.Keys.First();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(key, clonedKey)).IsFalse();
            await Assert.That(key.Value).IsEqualTo(clonedKey.Value);

            await Assert.That(originalDict.ContainsKey(key)).IsTrue();
            await Assert.That(clonedDict.ContainsKey(clonedKey)).IsTrue(); // important

            await Assert.That(clonedKey.Value is "TestKey").IsTrue();

            // Assert
        }
    }

    [Test]
    public async Task MultipleDictionariesAtSameLevelShouldBeClonedCorrectly()
    {
        // Arrange
        DictionaryContainer container = new DictionaryContainer
        {
            Dict1 = new Dictionary<KeyClass, string>(),
            Dict2 = new Dictionary<KeyClass, string>()
        };

        KeyClass key1 = new KeyClass { Value = "Key1" };
        KeyClass key2 = new KeyClass { Value = "Key2" };

        container.Dict1[key1] = "Value1";
        container.Dict2[key2] = "Value2";

        // Act
        DictionaryContainer clonedContainer = container.DeepClone();
        KeyClass clonedKey1 = clonedContainer.Dict1.Keys.First();
        KeyClass clonedKey2 = clonedContainer.Dict2.Keys.First();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(container.Dict1, clonedContainer.Dict1)).IsFalse();
            await Assert.That(ReferenceEquals(container.Dict2, clonedContainer.Dict2)).IsFalse();

            await Assert.That(ReferenceEquals(key1, clonedKey1)).IsFalse();
            await Assert.That(ReferenceEquals(key2, clonedKey2)).IsFalse();
            await Assert.That(key1.Value).IsEqualTo(clonedKey1.Value);
            await Assert.That(key2.Value).IsEqualTo(clonedKey2.Value);

            await Assert.That(container.Dict1.ContainsKey(key1)).IsTrue();
            await Assert.That(container.Dict2.ContainsKey(key2)).IsTrue();
            await Assert.That(clonedContainer.Dict1.ContainsKey(clonedKey1)).IsTrue();
            await Assert.That(clonedContainer.Dict2.ContainsKey(clonedKey2)).IsTrue();

            await Assert.That(clonedContainer.Dict1[clonedKey1]).IsEqualTo("Value1");
            await Assert.That(clonedContainer.Dict2[clonedKey2]).IsEqualTo("Value2");

            // Assert
        }
    }

    [Test]
    public async Task MultipleHashSetsAtSameLevelShouldBeClonedCorrectly()
    {
        // Arrange
        HashSetContainer container = new HashSetContainer
        {
            Set1 = [],
            Set2 = [],
            Set3 = []
        };

        KeyClass item1 = new KeyClass { Value = "Item1" };
        KeyClass item2 = new KeyClass { Value = "Item2" };
        KeyClass item3 = new KeyClass { Value = "Item3" };

        container.Set1.Add(item1);
        container.Set2.Add(item2);
        container.Set3.Add(item3);

        // Act
        HashSetContainer clonedContainer = container.DeepClone();
        KeyClass clonedItem1 = clonedContainer.Set1.First();
        KeyClass clonedItem2 = clonedContainer.Set2.First();
        KeyClass clonedItem3 = clonedContainer.Set3.First();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(container.Set1, clonedContainer.Set1)).IsFalse();
            await Assert.That(ReferenceEquals(container.Set2, clonedContainer.Set2)).IsFalse();
            await Assert.That(ReferenceEquals(container.Set3, clonedContainer.Set3)).IsFalse();

            await Assert.That(ReferenceEquals(item1, clonedItem1)).IsFalse();
            await Assert.That(ReferenceEquals(item2, clonedItem2)).IsFalse();
            await Assert.That(ReferenceEquals(item3, clonedItem3)).IsFalse();

            await Assert.That(item1.Value).IsEqualTo(clonedItem1.Value);
            await Assert.That(item2.Value).IsEqualTo(clonedItem2.Value);
            await Assert.That(item3.Value).IsEqualTo(clonedItem3.Value);

            await Assert.That(container.Set1).Contains(item1);
            await Assert.That(container.Set2).Contains(item2);
            await Assert.That(container.Set3).Contains(item3);

            await Assert.That(clonedContainer.Set1).Contains(clonedItem1);
            await Assert.That(clonedContainer.Set2).Contains(clonedItem2);
            await Assert.That(clonedContainer.Set3).Contains(clonedItem3);

            await Assert.That(container.Set1).Count().IsEqualTo(1);
            await Assert.That(container.Set2).Count().IsEqualTo(1);
            await Assert.That(container.Set3).Count().IsEqualTo(1);

            await Assert.That(clonedContainer.Set1).Count().IsEqualTo(1);
            await Assert.That(clonedContainer.Set2).Count().IsEqualTo(1);
            await Assert.That(clonedContainer.Set3).Count().IsEqualTo(1);

            // Assert
        }
    }

    public class HashSetContainer
    {
        public HashSet<KeyClass> Set1 { get; set; } = null!;
        public HashSet<KeyClass> Set2 { get; set; } = null!;
        public HashSet<KeyClass> Set3 { get; set; } = null!;
    }

    public class DictionaryContainer
    {
        public Dictionary<KeyClass, string> Dict1 { get; set; } = null!;
        public Dictionary<KeyClass, string> Dict2 { get; set; } = null!;
    }

    [Test]
    public async Task NestedDictionariesShouldBeClonedCorrectly()
    {
        // Arrange
        Dictionary<string, Dictionary<KeyClass, string>> outerDict = new Dictionary<string, Dictionary<KeyClass, string>>();
        Dictionary<KeyClass, string> innerDict = new Dictionary<KeyClass, string>();
        KeyClass key = new KeyClass { Value = "TestKey" };
        innerDict[key] = "TestValue";
        outerDict["outer"] = innerDict;

        // Act
        Dictionary<string, Dictionary<KeyClass, string>> clonedOuterDict = outerDict.DeepClone();
        Dictionary<KeyClass, string> clonedInnerDict = clonedOuterDict["outer"];
        KeyClass clonedKey = clonedInnerDict.Keys.First();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(outerDict, clonedOuterDict)).IsFalse().Because("Outer dictionaries should be different instances");
            await Assert.That(ReferenceEquals(innerDict, clonedInnerDict)).IsFalse().Because("Inner dictionaries should be different instances");

            await Assert.That(ReferenceEquals(key, clonedKey)).IsFalse().Because("Keys should be different instances");
            await Assert.That(key.Value).IsEqualTo(clonedKey.Value).Because("Key values should be equal");

            await Assert.That(innerDict.ContainsKey(key)).IsTrue().Because("Original inner dict should contain original key");
            await Assert.That(clonedInnerDict.ContainsKey(clonedKey)).IsTrue().Because("Cloned inner dict should contain cloned key");

            await Assert.That(innerDict[key]).IsEqualTo("TestValue").Because("Original value should be preserved");
            await Assert.That(clonedInnerDict[clonedKey]).IsEqualTo("TestValue").Because("Cloned value should be equal");

            await Assert.That(outerDict.Keys).Count().IsEqualTo(1).Because("Original outer dict should have one key");
            await Assert.That(clonedOuterDict.Keys).Count().IsEqualTo(1).Because("Cloned outer dict should have one key");
            await Assert.That(innerDict.Keys).Count().IsEqualTo(1).Because("Original inner dict should have one key");
            await Assert.That(clonedInnerDict.Keys).Count().IsEqualTo(1).Because("Cloned inner dict should have one key");

            // Assert
        }
    }


    [Test]
    public async Task SetBrokenAfterCloningTest()
    {
        // Arrange
        HashSet<KeyClass> originalSet = [];
        KeyClass key = new KeyClass { Value = "TestKey" };
        originalSet.Add(key);

        // Act
        HashSet<KeyClass> clonedSet = originalSet.DeepClone();
        KeyClass clonedKey = clonedSet.First();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(key, clonedKey)).IsFalse();
            await Assert.That(key.Value).IsEqualTo(clonedKey.Value);
            await Assert.That(originalSet).Contains(key);
            await Assert.That(clonedSet).Contains(clonedKey); // important
            await Assert.That(clonedKey.Value is "TestKey").IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task CanCopyTypes()
    {
        Type original = typeof(string);
        Type result = original.DeepClone();
        await Assert.That(original).IsEqualTo(result);
    }

    [Test]
    public async Task OrderedDictionaryBrokenAfterCloningTest()
    {
        // Arrange
        OrderedDictionary originalDict = new OrderedDictionary();
        KeyClass key = new KeyClass { Value = "TestKey" };
        originalDict[key] = "TestValue";

        // Act
        OrderedDictionary clonedDict = originalDict.DeepClone();
        KeyClass? clonedKey = clonedDict.Keys.Cast<KeyClass>().First();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(key, clonedKey)).IsFalse();
            await Assert.That(key.Value).IsEqualTo(clonedKey.Value);
            await Assert.That(originalDict.Contains(key)).IsTrue();
            await Assert.That(clonedDict.Contains(clonedKey)).IsTrue(); // important
            await Assert.That(clonedKey.Value is "TestKey").IsTrue();
            await Assert.That(clonedDict[clonedKey]).IsEqualTo("TestValue");

            // Assert
        }
    }

    [Test]
    public async Task TaskCancelledExceptionCloningTest()
    {
        // Arrange
        CancellationTokenSource cts = new CancellationTokenSource();
        TaskCanceledException? originalException = null;
    
        try
        {
            // Create a canceled task that will throw TaskCancelledException
            cts.Cancel();
            Task.Delay(100, cts.Token).GetAwaiter().GetResult();
        }
        catch (TaskCanceledException ex)
        {
            originalException = ex;
        }

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(originalException).IsNotNull();

            await Assert.That(async () =>
            {
                TaskCanceledException? clonedException = originalException.DeepClone();
                await Assert.That(clonedException).IsNotNull();
                await Assert.That(clonedException.Message).IsEqualTo(originalException.Message);
                await Assert.That(ReferenceEquals(originalException, clonedException)).IsFalse();
                await Assert.That((object?)clonedException.Task).IsNotNull();
            }).ThrowsNothing();

            // Act & Assert
        }
        
        cts.Dispose();
    }

    [Test]
    public async Task SingleGenericDictionaryCloneTest()
    {
        // Arrange
        SingleGenericDictionary<KeyValuePair<KeyClass, string>> originalDict = new SingleGenericDictionary<KeyValuePair<KeyClass, string>>();
        KeyClass key = new KeyClass { Value = "TestKey" };
        KeyValuePair<KeyClass, string> kvp = new KeyValuePair<KeyClass, string>(key, "TestValue");
        originalDict.Add(kvp);

        // Act
        SingleGenericDictionary<KeyValuePair<KeyClass, string>> clonedDict = originalDict.DeepClone();
        KeyValuePair<KeyClass, string> clonedKvp = clonedDict.First();
        KeyClass clonedKey = clonedKvp.Key;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReferenceEquals(key, clonedKey)).IsFalse();
            await Assert.That(key.Value).IsEqualTo(clonedKey.Value);

            await Assert.That(originalDict.Count).IsEqualTo(clonedDict.Count);
            await Assert.That(clonedDict.Contains(clonedKvp)).IsTrue();

            await Assert.That(clonedKey.Value).IsEqualTo("TestKey");
            await Assert.That(clonedKvp.Value).IsEqualTo("TestValue");

            // Assert
        }
    }

    public class SingleGenericDictionary<T> : Collection<T>, IDictionary
    {
        public object? this[object key]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public bool IsFixedSize => false;
        public bool IsReadOnly => false;
        public ICollection Keys => throw new NotImplementedException();
        public ICollection Values => throw new NotImplementedException();
        public void Add(object key, object? value)
        {
            if (key is not T typedKey)
                throw new ArgumentException($"Key must be of type {typeof(T).Name}", nameof(key));

            Add((T)key);
        }
        public void Clear() => base.Clear();
        public bool Contains(object key) => Items.Contains((T)key);
        public IDictionaryEnumerator GetEnumerator() => throw new NotImplementedException();
        public void Remove(object key) => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => base.GetEnumerator();
    }

    public interface IActivityDataWithProp
    {
        int Test { get; set; }
    }

    public class SampleInterfaceClsWithProp
    {
        [Newtonsoft.Json.JsonIgnore]
        public IActivityDataWithProp? ActivityData { get; set; }

        public SampleInterfaceClsWithProp()
        {

        }

        public SampleInterfaceClsWithProp(IActivityDataWithProp data) => SetActivityData(data);

        public void SetActivityData(IActivityDataWithProp data) => ActivityData = data;
    }

    public class SampleActivityDataWithProp : IActivityDataWithProp
    {
        public int Test { get; set; } = 42;
        public SampleActivityParsedData Data { get; set; }
    }

    public class SampleActivityParsedData
    {
        public List<string> Steps { get; set; } = [];
    }

    public class C1
    {
        public int A { get; set; }

        public virtual string B { get; set; }

        public byte[] C { get; set; }
    }

    public class C2 : C1
    {
        public decimal D { get; set; }

        public new int A { get; set; }
    }

    public class C4 : C1
    {
    }

    public class C3
    {
        public C1 A { get; set; }

        public C1 B { get; set; }
    }

    public interface I1
    {
        int A { get; set; }
    }

    public struct S1 : I1
    {
        public int A { get; set; }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Simple_Class_Should_Be_Cloned(bool isDeep)
    {
        C1 cFrom = new C1
        {
            A = 12,
            B = "testestest",
            C = [1, 2, 3]
        };

        C1 cTo = new C1
        {
            A = 11,
            B = "tes",
            C = [1]
        };

        C1 cToRef = cTo;

        if (isDeep)
            cFrom.DeepCloneTo(cTo);
        else
            cFrom.ShallowCloneTo(cTo);

        await Assert.That(ReferenceEquals(cTo, cToRef)).IsTrue();
        await Assert.That(cTo.A).IsEqualTo(12);
        await Assert.That(cTo.B).IsEqualTo("testestest");
        await Assert.That(cTo.C.Length).IsEqualTo(3);
        await Assert.That(cTo.C[2]).IsEqualTo((byte)3);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Descendant_Class_Should_Be_Cloned(bool isDeep)
    {
        C1 cFrom = new C1
        {
            A = 12,
            B = "testestest",
            C = [1, 2, 3]
        };

        C2 cTo = new C2
        {
            A = 11,
            D = 42.3m
        };

        if (isDeep)
            cFrom.DeepCloneTo(cTo);
        else
            cFrom.ShallowCloneTo(cTo);

        await Assert.That(ReferenceEquals(cTo, cTo)).IsTrue();
        await Assert.That(cTo.A).IsEqualTo(11);
        await Assert.That(((C1)cTo).A).IsEqualTo(12);
        await Assert.That(cTo.D).IsEqualTo(42.3m);
    }

    [Test]
    public async Task Class_With_Subclass_Should_Be_Shallow_CLoned()
    {
        C1 c1 = new C1 { A = 12 };
        C3 cFrom = new C3 { A = c1, B = c1 };
        C3 cTo = cFrom.ShallowCloneTo(new C3());
        await Assert.That(ReferenceEquals(cFrom.A, cTo.A)).IsTrue();
        await Assert.That(ReferenceEquals(cFrom.B, cTo.B)).IsTrue();
        await Assert.That(ReferenceEquals(cTo.A, cTo.B)).IsTrue();
    }

    [Test]
    public async Task Class_With_Subclass_Should_Be_Deep_CLoned()
    {
        C1 c1 = new C1 { A = 12 };
        C3 cFrom = new C3 { A = c1, B = c1 };
        C3 cTo = cFrom.DeepCloneTo(new C3());
        await Assert.That(ReferenceEquals(cFrom.A, cTo.A)).IsFalse();
        await Assert.That(ReferenceEquals(cFrom.B, cTo.B)).IsFalse();
        await Assert.That(ReferenceEquals(cTo.A, cTo.B)).IsTrue();
    }

    [Test]
    [NotInParallel]
    public async Task DeepCloneTo_RuntimeMutations_ReflectAndRestoreOnConfiguredRail()
    {
        C1 shared = new C1 { A = 12, B = "shared" };
        C3 source = new C3 { A = shared, B = shared };

        C3 baseline = source.DeepCloneTo(new C3());
        await Assert.That(ReferenceEquals(source.A, baseline.A)).IsFalse();
        await Assert.That(ReferenceEquals(baseline.A, baseline.B)).IsTrue();

        try
        {
            FastCloner.SetTypeBehavior<C1>(CloneBehavior.Reference);
            C3 referenced = source.DeepCloneTo(new C3());
            await Assert.That(ReferenceEquals(source.A, referenced.A)).IsTrue();
            await Assert.That(ReferenceEquals(referenced.A, referenced.B)).IsTrue();
        }
        finally
        {
            FastCloner.ClearTypeBehavior<C1>();
        }

        C3 restored = source.DeepCloneTo(new C3());
        await Assert.That(ReferenceEquals(source.A, restored.A)).IsFalse();
        await Assert.That(ReferenceEquals(restored.A, restored.B)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Copy_To_Null_Should_Return_Null(bool isDeep)
    {
        C1 c1 = new C1();
        if (isDeep)
            await Assert.That(c1.DeepCloneTo((C1)null)).IsNull();
        else
            await Assert.That(c1.ShallowCloneTo((C1)null)).IsNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Copy_From_Null_Should_Throw_Error(bool isDeep)
    {
        C1 c1 = null;
        if (isDeep)
            // ReSharper disable once ExpressionIsAlwaysNull
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                c1.DeepCloneTo(new C1());
                return Task.CompletedTask;
            });
        else
            // ReSharper disable once ExpressionIsAlwaysNull
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
            {
                c1.ShallowCloneTo(new C1());
                return Task.CompletedTask;
            });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Invalid_Inheritance_Should_Throw_Error(bool isDeep)
    {
        C1 c1 = new C4();
        if (isDeep)
            // ReSharper disable once ExpressionIsAlwaysNull
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                c1.DeepCloneTo(new C2());
                return Task.CompletedTask;
            });
        else
            // ReSharper disable once ExpressionIsAlwaysNull
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                c1.ShallowCloneTo(new C2());
                return Task.CompletedTask;
            });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Struct_As_Interface_ShouldNot_Be_Cloned(bool isDeep)
    {
        S1 sFrom = new S1 { A = 42 };
        S1 sTo = new S1();
        I1? objTo = sTo;
        objTo.A = 23;
        if (isDeep)
            // ReSharper disable once ExpressionIsAlwaysNull
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                ((I1)sFrom).DeepCloneTo(objTo);
                return Task.CompletedTask;
            });
        else
            // ReSharper disable once ExpressionIsAlwaysNull
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                ((I1)sFrom).ShallowCloneTo(objTo);
                return Task.CompletedTask;
            });
    }

    [Test]
    public async Task String_Should_Not_Be_Cloned()
    {
        string s1 = "abc";
        string s2 = "def";
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
        {
            s1.ShallowCloneTo(s2);
            return Task.CompletedTask;
        });
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Array_Should_Be_Cloned_Correct_Size(bool isDeep)
    {
        int[] arrFrom = [1, 2, 3];
        int[] arrTo = [4, 5, 6];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(3);
        await Assert.That(arrTo[0]).IsEqualTo(1);
        await Assert.That(arrTo[1]).IsEqualTo(2);
        await Assert.That(arrTo[2]).IsEqualTo(3);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Array_Should_Be_Cloned_From_Is_Bigger(bool isDeep)
    {
        int[] arrFrom = [1, 2, 3];
        int[] arrTo = [4, 5];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(2);
        await Assert.That(arrTo[0]).IsEqualTo(1);
        await Assert.That(arrTo[1]).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Array_Should_Be_Cloned_From_Is_Smaller(bool isDeep)
    {
        int[] arrFrom = [1, 2];
        int[] arrTo = [4, 5, 6];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(3);
        await Assert.That(arrTo[0]).IsEqualTo(1);
        await Assert.That(arrTo[1]).IsEqualTo(2);
        await Assert.That(arrTo[2]).IsEqualTo(6);
    }

    [Test]
    public async Task Shallow_Array_Should_Be_Cloned()
    {
        C1 c1 = new C1();
        C1[] arrFrom = [c1, c1, c1];
        C1[] arrTo = new C1[4];
        arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(4);
        await Assert.That(arrTo[0]).IsEqualTo(c1);
        await Assert.That(arrTo[1]).IsEqualTo(c1);
        await Assert.That(arrTo[2]).IsEqualTo(c1);
        await Assert.That(arrTo[3]).IsNull();
    }

    [Test]
    public async Task Deep_Array_Should_Be_Cloned()
    {
        C4 c1 = new C4();
        C3 c3 = new C3 { A = c1, B = c1 };
        C3[] arrFrom = [c3, c3, c3];
        C3[] arrTo = new C3[4];
        arrFrom.DeepCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(4);
#pragma warning disable NUnit2021 // Incompatible types for EqualTo constraint
        await Assert.That(arrTo[0]).IsNotSameReferenceAs(c1);
#pragma warning restore NUnit2021 // Incompatible types for EqualTo constraint
        await Assert.That(arrTo[0]).IsEqualTo(arrTo[1]);
        await Assert.That(arrTo[1]).IsEqualTo(arrTo[2]);
        await Assert.That(ReferenceEquals(arrTo[2].A, c1)).IsFalse();
        await Assert.That(arrTo[2].A).IsEqualTo(arrTo[2].B);
        await Assert.That(arrTo[3]).IsNull();
    }

    public struct StructWithRef
    {
        public C1 Ref;
        public int Marker;
    }

    [Test]
    public async Task Deep_Array_Of_Struct_With_Ref_Should_Use_Source_Values()
    {
        StructWithRef[] arrFrom =
        [
            new StructWithRef { Ref = new C1 { A = 10, B = "from-1", C = [1] }, Marker = 101 },
            new StructWithRef { Ref = new C1 { A = 20, B = "from-2", C = [2] }, Marker = 202 }
        ];

        // Pre-populate target with different values; old bug cloned from target instead of source.
        StructWithRef[] arrTo =
        [
            new StructWithRef { Ref = new C1 { A = -1, B = "to-1", C = [9] }, Marker = -101 },
            new StructWithRef { Ref = new C1 { A = -2, B = "to-2", C = [8] }, Marker = -202 }
        ];

        arrFrom.DeepCloneTo(arrTo);

        using (Assert.Multiple())
        {
            await Assert.That(arrTo[0].Marker).IsEqualTo(101);
            await Assert.That(arrTo[1].Marker).IsEqualTo(202);
            await Assert.That(arrTo[0].Ref.A).IsEqualTo(10);
            await Assert.That(arrTo[1].Ref.A).IsEqualTo(20);
            await Assert.That(arrTo[0].Ref.B).IsEqualTo("from-1");
            await Assert.That(arrTo[1].Ref.B).IsEqualTo("from-2");
            await Assert.That(arrTo[0].Ref).IsNotSameReferenceAs(arrFrom[0].Ref);
            await Assert.That(arrTo[1].Ref).IsNotSameReferenceAs(arrFrom[1].Ref);

        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Non_Zero_Based_Array_Should_Be_Cloned(bool isDeep)
    {
        Array arrFrom = Array.CreateInstance(typeof(int),
            [2],
            [1]);
        // with offset. its ok
        Array arrTo = Array.CreateInstance(typeof(int),
            [2],
            [0]);
        arrFrom.SetValue(1, 1);
        arrFrom.SetValue(2, 2);
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(2);
        await Assert.That(arrTo.GetValue(0)).IsEqualTo(1);
        await Assert.That(arrTo.GetValue(1)).IsEqualTo(2);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task MultiDim_Array_Should_Be_Cloned(bool isDeep)
    {
        Array arrFrom = Array.CreateInstance(typeof(int),
            [2, 2],
            [1, 1]);
        // with offset. its ok
        Array arrTo = Array.CreateInstance(typeof(int),
            [1, 1],
            [0, 0]);
        arrFrom.SetValue(1, 1, 1);
        arrFrom.SetValue(2, 2, 2);
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo.Length).IsEqualTo(1);
        await Assert.That(arrTo.GetValue(0, 0)).IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TwoDim_Array_Should_Be_Cloned(bool isDeep)
    {
        int[,] arrFrom = { { 1, 2 }, { 3, 4 } };
        // with offset. its ok
        int[,] arrTo = new int[3, 1];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo[0, 0]).IsEqualTo(1);
        await Assert.That(arrTo[1, 0]).IsEqualTo(3);

        arrTo = new int[2, 2];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(arrTo[0, 0]).IsEqualTo(1);
        await Assert.That(arrTo[0, 1]).IsEqualTo(2);
        await Assert.That(arrTo[1, 0]).IsEqualTo(3);
    }

    [Test]
    public async Task MultiDim_Array_Should_Be_Cloned3()
    {
        const int cnt1 = 4;
        const int cnt2 = 5;
        const int cnt3 = 6;
        int[,,] arr = new int[cnt1, cnt2, cnt3];
        for (int i1 = 0; i1 < cnt1; i1++)
            for (int i2 = 0; i2 < cnt2; i2++)
                for (int i3 = 0; i3 < cnt3; i3++)
                    arr[i1, i2, i3] = i1 * 100 + i2 * 10 + i3;
        int[,,] clone = arr.DeepCloneTo(new int[cnt1, cnt2, cnt3]);
        await Assert.That(ReferenceEquals(arr, clone)).IsFalse();
        for (int i1 = 0; i1 < cnt1; i1++)
            for (int i2 = 0; i2 < cnt2; i2++)
                for (int i3 = 0; i3 < cnt3; i3++)
                    await Assert.That(arr[i1, i2, i3]).IsEqualTo(i1 * 100 + i2 * 10 + i3);
    }

    [Test]
    public void MultiDimensional_Array_Should_Be_Cloned()
    {
        // Issue #25
        Array.CreateInstance(typeof(int), [0, 0]).DeepCloneTo(new int[0, 0]);
        Array.CreateInstance(typeof(int), [1, 0]).DeepCloneTo(new int[1, 0]);
        Array.CreateInstance(typeof(int), [0, 1]).DeepCloneTo(new int[0, 1]);
        Array.CreateInstance(typeof(int), [1, 1]).DeepCloneTo(new int[1, 1]);

        Array.CreateInstance(typeof(int), [0, 0, 0]).DeepCloneTo(new int[0, 0, 0]);
        Array.CreateInstance(typeof(int), [1, 0, 0]).DeepCloneTo(new int[1, 0, 0]);
        Array.CreateInstance(typeof(int), [0, 1, 0]).DeepCloneTo(new int[0, 1, 0]);
        Array.CreateInstance(typeof(int), [0, 0, 1]).DeepCloneTo(new int[0, 0, 1]);
        Array.CreateInstance(typeof(int), [1, 1, 1]).DeepCloneTo(new int[1, 1, 1]);
    }

    [Test]
    public async Task Shallow_Clone_Of_MultiDim_Array_Should_Not_Perform_Deep()
    {
        C1 c1 = new C1();
        C1[,] arrFrom = { { c1, c1 }, { c1, c1 } };
        // with offset. its ok
        C1[,] arrTo = new C1[3, 1];
        arrFrom.ShallowCloneTo(arrTo);
        await Assert.That(ReferenceEquals(c1, arrTo[0, 0])).IsTrue();
        await Assert.That(ReferenceEquals(c1, arrTo[1, 0])).IsTrue();

        C1[,,] arrFrom2 = new C1[1, 1, 1];
        arrFrom2[0, 0, 0] = c1;
        C1[,,] arrTo2 = new C1[1, 1, 1];
        arrFrom2.ShallowCloneTo(arrTo2);
        await Assert.That(ReferenceEquals(c1, arrTo2[0, 0, 0])).IsTrue();
    }

    [Test]
    public async Task Deep_Clone_Of_MultiDim_Array_Should_Perform_Deep()
    {
        C1 c1 = new C1();
        C1[,] arrFrom = { { c1, c1 }, { c1, c1 } };
        // with offset. its ok
        C1[,] arrTo = new C1[3, 1];
        arrFrom.DeepCloneTo(arrTo);
        await Assert.That(ReferenceEquals(c1, arrTo[0, 0])).IsFalse();
        await Assert.That(ReferenceEquals(arrTo[0, 0], arrTo[1, 0])).IsTrue();

        C1[,,] arrFrom2 = new C1[1, 1, 2];
        arrFrom2[0, 0, 0] = c1;
        arrFrom2[0, 0, 1] = c1;
        C1[,,] arrTo2 = new C1[1, 1, 2];
        arrFrom2.DeepCloneTo(arrTo2);
        await Assert.That(ReferenceEquals(c1, arrTo2[0, 0, 0])).IsFalse();
        await Assert.That(ReferenceEquals(arrTo2[0, 0, 1], arrTo2[0, 0, 0])).IsTrue();
    }

    [Test]
    public async Task Dictionary_Should_Be_Deeply_Cloned()
    {
        Dictionary<string, string> d1 = new Dictionary<string, string>{ { "A", "B" }, { "C", "D" } };
        Dictionary<string, string> d2 = new Dictionary<string, string>();
        d1.DeepCloneTo(d2);
        d1["A"] = "E";
        await Assert.That(d2.Count).IsEqualTo(2);
        await Assert.That(d2["A"]).IsEqualTo("B");
        await Assert.That(d2["C"]).IsEqualTo("D");

        // big dictionary
        d1.Clear();
        for (int i = 0; i < 1000; i++)
            d1[i.ToString()] = i.ToString();
        d1.DeepCloneTo(d2);
        await Assert.That(d2.Count).IsEqualTo(1000);
        await Assert.That(d2["557"]).IsEqualTo("557");
    }

    public class D1
    {
        public int A { get; set; }
    }

    public class D2 : D1
    {
        public int B { get; set; }

        public D2(D1 d1)
        {
            B = 14;
            d1.DeepCloneTo(this);
        }
    }

    [Test]
    public async Task Inner_Implementation_In_Class_Should_Work()
    {
        D1 baseObject = new D1 { A = 12 };
        D2 wrapper = new D2(baseObject);
        await Assert.That(wrapper.A).IsEqualTo(12);
        await Assert.That(wrapper.B).IsEqualTo(14);
    }
    
    [Test]
    public async Task DictionaryWithStringKeys_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - string keys have stable hash semantics (fast path)
        Dictionary<string, int> original = new Dictionary<string, int>
        {
            ["one"] = 1,
            ["two"] = 2,
            ["three"] = 3
        };
        
        // Act
        Dictionary<string, int> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(3);
            await Assert.That(cloned["one"]).IsEqualTo(1);
            await Assert.That(cloned["two"]).IsEqualTo(2);
            await Assert.That(cloned["three"]).IsEqualTo(3);
            await Assert.That(cloned.ContainsKey("one")).IsTrue();
            await Assert.That(cloned.ContainsKey("two")).IsTrue();
            await Assert.That(cloned.ContainsKey("three")).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task DictionaryWithIntKeys_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - int keys have stable hash semantics (fast path)
        Dictionary<int, string> original = new Dictionary<int, string>
        {
            [1] = "one",
            [2] = "two",
            [100] = "hundred"
        };
        
        // Act
        Dictionary<int, string> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(3);
            await Assert.That(cloned[1]).IsEqualTo("one");
            await Assert.That(cloned[2]).IsEqualTo("two");
            await Assert.That(cloned[100]).IsEqualTo("hundred");
            await Assert.That(cloned.ContainsKey(1)).IsTrue();
            await Assert.That(cloned.ContainsKey(2)).IsTrue();
            await Assert.That(cloned.ContainsKey(100)).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task DictionaryWithGuidKeys_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - Guid keys have stable hash semantics (fast path)
        Guid key1 = Guid.NewGuid();
        Guid key2 = Guid.NewGuid();
        
        Dictionary<Guid, string> original = new Dictionary<Guid, string>
        {
            [key1] = "value1",
            [key2] = "value2"
        };
        
        // Act
        Dictionary<Guid, string> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(2);
            await Assert.That(cloned[key1]).IsEqualTo("value1");
            await Assert.That(cloned[key2]).IsEqualTo("value2");
            await Assert.That(cloned.ContainsKey(key1)).IsTrue();
            await Assert.That(cloned.ContainsKey(key2)).IsTrue();

            // Assert
        }
    }
    
    public record RecordKey(string Name, int Id);
    
    [Test]
    public async Task DictionaryWithRecordKeys_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - record keys have compiler-generated GetHashCode (fast path)
        RecordKey key1 = new RecordKey("Alice", 1);
        RecordKey key2 = new RecordKey("Bob", 2);
        
        Dictionary<RecordKey, string> original = new Dictionary<RecordKey, string>
        {
            [key1] = "data1",
            [key2] = "data2"
        };
        
        // Act
        Dictionary<RecordKey, string> cloned = original.DeepClone();
        RecordKey clonedKey1 = cloned.Keys.First(k => k.Name == "Alice");
        RecordKey clonedKey2 = cloned.Keys.First(k => k.Name == "Bob");
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(2);

            // Records should be cloned (not same reference)
            await Assert.That(clonedKey1).IsNotSameReferenceAs(key1);
            await Assert.That(clonedKey2).IsNotSameReferenceAs(key2);

            // But should still be equal (value equality)
            await Assert.That(clonedKey1).IsEqualTo(key1);
            await Assert.That(clonedKey2).IsEqualTo(key2);

            // Dictionary lookups should work
            await Assert.That(cloned.ContainsKey(clonedKey1)).IsTrue();
            await Assert.That(cloned.ContainsKey(clonedKey2)).IsTrue();
            await Assert.That(cloned[clonedKey1]).IsEqualTo("data1");
            await Assert.That(cloned[clonedKey2]).IsEqualTo("data2");

            // Assert
        }
    }
    
    public struct StructKey
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    
    [Test]
    public async Task DictionaryWithStructKeys_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - struct keys have value-based hash (fast path)
        StructKey key1 = new StructKey { Id = 1, Name = "One" };
        StructKey key2 = new StructKey { Id = 2, Name = "Two" };
        
        Dictionary<StructKey, string> original = new Dictionary<StructKey, string>
        {
            [key1] = "value1",
            [key2] = "value2"
        };
        
        // Act
        Dictionary<StructKey, string> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(2);
            await Assert.That(cloned.ContainsKey(key1)).IsTrue();
            await Assert.That(cloned.ContainsKey(key2)).IsTrue();
            await Assert.That(cloned[key1]).IsEqualTo("value1");
            await Assert.That(cloned[key2]).IsEqualTo("value2");

            // Assert
        }
    }
    
    [Test]
    public async Task HashSetWithStrings_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - string elements have stable hash semantics (fast path)
        HashSet<string> original = ["apple", "banana", "cherry"];
        
        // Act
        HashSet<string> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(3);
            await Assert.That(cloned.Contains("apple")).IsTrue();
            await Assert.That(cloned.Contains("banana")).IsTrue();
            await Assert.That(cloned.Contains("cherry")).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task HashSetWithInts_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - int elements have stable hash semantics (fast path)
        HashSet<int> original = [1, 2, 3, 100, 999];
        
        // Act
        HashSet<int> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(5);
            await Assert.That(cloned.Contains(1)).IsTrue();
            await Assert.That(cloned.Contains(2)).IsTrue();
            await Assert.That(cloned.Contains(3)).IsTrue();
            await Assert.That(cloned.Contains(100)).IsTrue();
            await Assert.That(cloned.Contains(999)).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task HashSetWithRecords_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - record elements have compiler-generated GetHashCode (fast path)
        RecordKey item1 = new RecordKey("Alice", 1);
        RecordKey item2 = new RecordKey("Bob", 2);
        
        HashSet<RecordKey> original = [item1, item2];
        
        // Act
        HashSet<RecordKey> cloned = original.DeepClone();
        RecordKey clonedItem1 = cloned.First(k => k.Name == "Alice");
        RecordKey clonedItem2 = cloned.First(k => k.Name == "Bob");
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(2);

            // Records should be cloned (not same reference)
            await Assert.That(clonedItem1).IsNotSameReferenceAs(item1);
            await Assert.That(clonedItem2).IsNotSameReferenceAs(item2);

            // But should still be equal (value equality)
            await Assert.That(clonedItem1).IsEqualTo(item1);
            await Assert.That(clonedItem2).IsEqualTo(item2);

            // Set lookups should work
            await Assert.That(cloned.Contains(clonedItem1)).IsTrue();
            await Assert.That(cloned.Contains(clonedItem2)).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task DictionaryWithReferenceKeys_SlowPath_ShouldCloneCorrectly()
    {
        // Arrange - KeyClass uses default GetHashCode (identity-based, slow path)
        KeyClass key1 = new KeyClass { Value = "Key1" };
        KeyClass key2 = new KeyClass { Value = "Key2" };
        
        Dictionary<KeyClass, int> original = new Dictionary<KeyClass, int>
        {
            [key1] = 100,
            [key2] = 200
        };
        
        // Act
        Dictionary<KeyClass, int> cloned = original.DeepClone();
        KeyClass clonedKey1 = cloned.Keys.First(k => k.Value == "Key1");
        KeyClass clonedKey2 = cloned.Keys.First(k => k.Value == "Key2");
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(2);

            // Keys should be cloned (not same reference)
            await Assert.That(clonedKey1).IsNotSameReferenceAs(key1);
            await Assert.That(clonedKey2).IsNotSameReferenceAs(key2);

            // Dictionary lookups should work with cloned keys (slow path ensures this)
            await Assert.That(cloned.ContainsKey(clonedKey1)).IsTrue();
            await Assert.That(cloned.ContainsKey(clonedKey2)).IsTrue();
            await Assert.That(cloned[clonedKey1]).IsEqualTo(100);
            await Assert.That(cloned[clonedKey2]).IsEqualTo(200);

            // Assert
        }
    }
    
    [Test]
    public async Task HashSetWithReferenceElements_SlowPath_ShouldCloneCorrectly()
    {
        // Arrange - KeyClass uses default GetHashCode (identity-based, slow path)
        KeyClass item1 = new KeyClass { Value = "Item1" };
        KeyClass item2 = new KeyClass { Value = "Item2" };
        
        HashSet<KeyClass> original = [item1, item2];
        
        // Act
        HashSet<KeyClass> cloned = original.DeepClone();
        KeyClass clonedItem1 = cloned.First(k => k.Value == "Item1");
        KeyClass clonedItem2 = cloned.First(k => k.Value == "Item2");
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(2);

            // Elements should be cloned (not same reference)
            await Assert.That(clonedItem1).IsNotSameReferenceAs(item1);
            await Assert.That(clonedItem2).IsNotSameReferenceAs(item2);

            // Set lookups should work with cloned elements (slow path ensures this)
            await Assert.That(cloned.Contains(clonedItem1)).IsTrue();
            await Assert.That(cloned.Contains(clonedItem2)).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task LargeDictionary_FastPath_ShouldCloneEfficiently()
    {
        // Arrange - large dictionary with string keys (fast path)
        Dictionary<string, int> original = new Dictionary<string, int>();
        for (int i = 0; i < 10000; i++)
        {
            original[$"key_{i}"] = i;
        }
        
        // Act
        Dictionary<string, int> cloned = original.DeepClone();
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cloned).IsNotSameReferenceAs(original);
            await Assert.That(cloned.Count).IsEqualTo(10000);

            // Verify some random entries
            await Assert.That(cloned["key_0"]).IsEqualTo(0);
            await Assert.That(cloned["key_5000"]).IsEqualTo(5000);
            await Assert.That(cloned["key_9999"]).IsEqualTo(9999);
            await Assert.That(cloned.ContainsKey("key_1234")).IsTrue();

            // Assert
        }
    }
    
    [Test]
    public async Task LargeExpandoObject_ShouldCloneCorrectly()
    {
        // Arrange - this is the "Large" benchmark scenario (100 properties of various types)
        dynamic original = new System.Dynamic.ExpandoObject();
        IDictionary<string, object?> dict = original;
        
        for (int i = 0; i < 100; i++)
        {
            switch (i % 5)
            {
                case 0:
                    dict[$"StringProp_{i}"] = $"String value {i}";
                    break;
                case 1:
                    dict[$"IntProp_{i}"] = i * 10;
                    break;
                case 2:
                    dict[$"DoubleProp_{i}"] = i * 1.5;
                    break;
                case 3:
                    dict[$"DateProp_{i}"] = DateTime.UtcNow.AddDays(i);
                    break;
                case 4:
                    dict[$"NestedProp_{i}"] = new { NestedId = i, NestedValue = $"Nested {i}" };
                    break;
            }
        }
        
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
        IDictionary<string, object?> clonedDict = cloned;
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object)cloned).IsNotSameReferenceAs((object)original);
            await Assert.That(clonedDict.Count).IsEqualTo(100);

            // Verify string properties
            await Assert.That(clonedDict["StringProp_0"]).IsEqualTo("String value 0");
            await Assert.That(clonedDict["StringProp_50"]).IsEqualTo("String value 50");

            // Verify int properties
            await Assert.That(clonedDict["IntProp_1"]).IsEqualTo(10);
            await Assert.That(clonedDict["IntProp_51"]).IsEqualTo(510);

            // Verify double properties  
            await Assert.That(clonedDict["DoubleProp_2"]).IsEqualTo(3.0);
            await Assert.That(clonedDict["DoubleProp_52"]).IsEqualTo(78.0);

            // Verify nested anonymous types are cloned
            dynamic nested4 = clonedDict["NestedProp_4"];
            await Assert.That((int)nested4.NestedId).IsEqualTo(4);
            await Assert.That((string)nested4.NestedValue).IsEqualTo("Nested 4");

            dynamic nested99 = clonedDict["NestedProp_99"];
            await Assert.That((int)nested99.NestedId).IsEqualTo(99);
            await Assert.That((string)nested99.NestedValue).IsEqualTo("Nested 99");

            // Assert
        }
    }
    
    [Test]
    public async Task LargeExpandoObject_WithNestedExpandos_ShouldCloneCorrectly()
    {
        // Arrange - nested ExpandoObjects similar to benchmark
        dynamic root = new System.Dynamic.ExpandoObject();
        root.Name = "Root";
        root.Level = 0;
        
        dynamic child1 = new System.Dynamic.ExpandoObject();
        child1.Name = "Child1";
        child1.Level = 1;
        child1.Data = "Some data for child 1";
        
        dynamic grandchild = new System.Dynamic.ExpandoObject();
        grandchild.Name = "Grandchild";
        grandchild.Level = 2;
        grandchild.Tags = new[] { "tag1", "tag2", "tag3" };
        
        child1.Child = grandchild;
        root.Children = new List<object> { child1 };
        
        // Act
        dynamic cloned = FastCloner.DeepClone(root);
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object)cloned).IsNotSameReferenceAs((object)root);
            await Assert.That((string)cloned.Name).IsEqualTo("Root");
            await Assert.That((int)cloned.Level).IsEqualTo(0);

            // Verify nested structure is cloned
            List<object> clonedChildren = cloned.Children;
            await Assert.That(clonedChildren).IsNotSameReferenceAs((List<object>)root.Children);
            await Assert.That(clonedChildren.Count).IsEqualTo(1);

            dynamic clonedChild1 = clonedChildren[0];
            await Assert.That((object)clonedChild1).IsNotSameReferenceAs((object)child1);
            await Assert.That((string)clonedChild1.Name).IsEqualTo("Child1");

            dynamic clonedGrandchild = clonedChild1.Child;
            await Assert.That((object)clonedGrandchild).IsNotSameReferenceAs((object)grandchild);
            await Assert.That((string)clonedGrandchild.Name).IsEqualTo("Grandchild");

            string[] clonedTags = clonedGrandchild.Tags;
            await Assert.That(clonedTags).IsNotSameReferenceAs((string[])grandchild.Tags);
            await Assert.That(clonedTags).IsEquivalentTo(["tag1", "tag2", "tag3"]);

            // Assert
        }
    }
    
    [Test]
    public async Task ExpandoObject_WithCircularReference_ShouldCloneCorrectly()
    {
        // Arrange - circular reference similar to benchmark
        dynamic parent = new System.Dynamic.ExpandoObject();
        parent.Name = "Parent";
        parent.Id = 1;
        
        dynamic child = new System.Dynamic.ExpandoObject();
        child.Name = "Child";
        child.Id = 2;
        child.Parent = parent;
        
        parent.Child = child;
        parent.Self = parent;
        
        // Act
        dynamic cloned = FastCloner.DeepClone(parent);
        
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That((object)cloned).IsNotSameReferenceAs((object)parent);
            await Assert.That((string)cloned.Name).IsEqualTo("Parent");
            await Assert.That((int)cloned.Id).IsEqualTo(1);

            // Verify circular reference is preserved
            dynamic clonedChild = cloned.Child;
            await Assert.That((object)clonedChild).IsNotSameReferenceAs((object)child);
            await Assert.That((string)clonedChild.Name).IsEqualTo("Child");

            // Child's parent should point to cloned parent, not original
            await Assert.That((object)clonedChild.Parent).IsSameReferenceAs((object)cloned);
            await Assert.That((object)clonedChild.Parent).IsNotSameReferenceAs((object)parent);

            // Self reference should point to cloned parent
            await Assert.That((object)cloned.Self).IsSameReferenceAs((object)cloned);
            await Assert.That((object)cloned.Self).IsNotSameReferenceAs((object)parent);

            // Assert
        }
    }
    
    [Test]
    public async Task LargeExpandoObject_BenchmarkScenario_VerifyDeepClone()
    {
        dynamic original = new System.Dynamic.ExpandoObject();
        IDictionary<string, object?> originalDict = original;
        
        for (int i = 0; i < 100; i++)
        {
            switch (i % 5)
            {
                case 0:
                    originalDict[$"StringProp_{i}"] = $"String value {i}";
                    break;
                case 1:
                    originalDict[$"IntProp_{i}"] = i * 10;
                    break;
                case 2:
                    originalDict[$"DoubleProp_{i}"] = i * 1.5;
                    break;
                case 3:
                    originalDict[$"DateProp_{i}"] = DateTime.UtcNow.AddDays(i);
                    break;
                case 4:
                    originalDict[$"NestedProp_{i}"] = new { NestedId = i, NestedValue = $"Nested {i}" };
                    break;
            }
        }
        
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
        IDictionary<string, object?> clonedDict = cloned;
        
        using (Assert.Multiple())
        {
            // 1. Clone is a different object instance
            await Assert.That((object)cloned).IsNotSameReferenceAs((object)original).Because("Clone must be a different object instance");

            // 2. All 100 properties are present
            await Assert.That(clonedDict.Count).IsEqualTo(100).Because("Clone must have all 100 properties");

            // 3. Modifying clone does NOT affect original (independence test)
            clonedDict["StringProp_0"] = "MODIFIED";
            await Assert.That(originalDict["StringProp_0"]).IsEqualTo("String value 0").Because("Modifying clone must not affect original string");

            clonedDict["IntProp_1"] = 99999;
            await Assert.That(originalDict["IntProp_1"]).IsEqualTo(10).Because("Modifying clone must not affect original int");

            // 4. Anonymous types are immutable, so sharing is correct - verify values match
            dynamic originalNested4 = originalDict["NestedProp_4"];
            dynamic clonedNested4 = clonedDict["NestedProp_4"];
            await Assert.That((int)clonedNested4.NestedId).IsEqualTo(4);
            await Assert.That((string)clonedNested4.NestedValue).IsEqualTo("Nested 4");

            // 5. Verify multiple nested values are correct
            dynamic clonedNested99 = clonedDict["NestedProp_99"];
            await Assert.That((int)clonedNested99.NestedId).IsEqualTo(99);
            await Assert.That((string)clonedNested99.NestedValue).IsEqualTo("Nested 99");

            // 6. Original values unchanged after all modifications
            await Assert.That(originalDict["StringProp_50"]).IsEqualTo("String value 50");
            await Assert.That(originalDict["IntProp_51"]).IsEqualTo(510);
            await Assert.That(originalDict["DoubleProp_52"]).IsEqualTo(78.0);

            // 7. Replacing nested property in clone doesn't affect original
            clonedDict["NestedProp_4"] = new { NestedId = 999, NestedValue = "Replaced" };
            await Assert.That((int)originalNested4.NestedId).IsEqualTo(4).Because("Original nested value unchanged after replacing in clone");

        }
    }
    
    [Test]
    public async Task LargeExpandoObject_ModifyClonedNestedObjects_OriginalUnchanged()
    {
        // Arrange - ExpandoObject with mutable nested objects
        dynamic original = new System.Dynamic.ExpandoObject();
        original.Name = "Root";
        original.MutableList = new List<string> { "Item1", "Item2", "Item3" };
        original.MutableDict = new Dictionary<string, int> { ["Key1"] = 1, ["Key2"] = 2 };
        original.NestedExpando = new System.Dynamic.ExpandoObject();
        original.NestedExpando.Value = "OriginalValue";
        
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
        
        // Modify the cloned nested objects
        cloned.MutableList.Add("NewItem");
        cloned.MutableList[0] = "ModifiedItem1";
        cloned.MutableDict["Key1"] = 999;
        cloned.MutableDict["NewKey"] = 100;
        cloned.NestedExpando.Value = "ModifiedValue";
        cloned.NestedExpando.NewProp = "AddedProperty";
        
        // Assert - original is completely unchanged
        using (Assert.Multiple())
        {
            // List modifications don't affect original
            List<string> originalList = original.MutableList;
            await Assert.That(originalList.Count).IsEqualTo(3).Because("Original list count unchanged");
            await Assert.That(originalList[0]).IsEqualTo("Item1").Because("Original list items unchanged");
            await Assert.That(originalList.Contains("NewItem")).IsFalse().Because("Original list doesn't have new item");

            // Dictionary modifications don't affect original
            Dictionary<string, int> originalDictionary = original.MutableDict;
            await Assert.That(originalDictionary["Key1"]).IsEqualTo(1).Because("Original dict values unchanged");
            await Assert.That(originalDictionary.ContainsKey("NewKey")).IsFalse().Because("Original dict doesn't have new key");
            await Assert.That(originalDictionary.Count).IsEqualTo(2).Because("Original dict count unchanged");

            // Nested ExpandoObject modifications don't affect original
            await Assert.That((string)original.NestedExpando.Value).IsEqualTo("OriginalValue").Because("Original nested expando value unchanged");

            IDictionary<string, object?> originalNestedDict = original.NestedExpando;
            await Assert.That(originalNestedDict.ContainsKey("NewProp")).IsFalse().Because("Original nested expando doesn't have new property");

            // Verify cloned values are actually modified
            await Assert.That((int)cloned.MutableList.Count).IsEqualTo(4);
            await Assert.That((int)cloned.MutableDict["Key1"]).IsEqualTo(999);
            await Assert.That((string)cloned.NestedExpando.Value).IsEqualTo("ModifiedValue");

            // Assert - original is completely unchanged
        }
    }
}