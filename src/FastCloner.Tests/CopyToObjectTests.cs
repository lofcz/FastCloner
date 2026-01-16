using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace FastCloner.Tests;

[TestFixture(Low)]
[TestFixture(High)]
public class CopyToObjectTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public void InterfaceTest()
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
        Assert.That(ReferenceEquals(source.ActivityData, to.ActivityData), Is.EqualTo(false));
    }

    public class KeyClass
    {
        public string Value { get; set; }
    }

    [Test]
    public void DictionaryBrokenAfterCloningTest()
    {
        // Arrange
        Dictionary<KeyClass, string> originalDict = new Dictionary<KeyClass, string>();
        KeyClass key = new KeyClass { Value = "TestKey" };
        originalDict[key] = "TestValue";

        // Act
        Dictionary<KeyClass, string> clonedDict = originalDict.DeepClone();
        KeyClass clonedKey = clonedDict.Keys.First();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(key, clonedKey), Is.False);
            Assert.That(key.Value, Is.EqualTo(clonedKey.Value));

            Assert.That(originalDict.ContainsKey(key), Is.True);
            Assert.That(clonedDict.ContainsKey(clonedKey), Is.True); // important

            Assert.That(clonedKey.Value is "TestKey", Is.True);
        });
    }

    [Test]
    public void MultipleDictionariesAtSameLevelShouldBeClonedCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(container.Dict1, clonedContainer.Dict1), Is.False);
            Assert.That(ReferenceEquals(container.Dict2, clonedContainer.Dict2), Is.False);

            Assert.That(ReferenceEquals(key1, clonedKey1), Is.False);
            Assert.That(ReferenceEquals(key2, clonedKey2), Is.False);
            Assert.That(key1.Value, Is.EqualTo(clonedKey1.Value));
            Assert.That(key2.Value, Is.EqualTo(clonedKey2.Value));

            Assert.That(container.Dict1.ContainsKey(key1), Is.True);
            Assert.That(container.Dict2.ContainsKey(key2), Is.True);
            Assert.That(clonedContainer.Dict1.ContainsKey(clonedKey1), Is.True);
            Assert.That(clonedContainer.Dict2.ContainsKey(clonedKey2), Is.True);

            Assert.That(clonedContainer.Dict1[clonedKey1], Is.EqualTo("Value1"));
            Assert.That(clonedContainer.Dict2[clonedKey2], Is.EqualTo("Value2"));
        });
    }

    [Test]
    public void MultipleHashSetsAtSameLevelShouldBeClonedCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(container.Set1, clonedContainer.Set1), Is.False);
            Assert.That(ReferenceEquals(container.Set2, clonedContainer.Set2), Is.False);
            Assert.That(ReferenceEquals(container.Set3, clonedContainer.Set3), Is.False);

            Assert.That(ReferenceEquals(item1, clonedItem1), Is.False);
            Assert.That(ReferenceEquals(item2, clonedItem2), Is.False);
            Assert.That(ReferenceEquals(item3, clonedItem3), Is.False);

            Assert.That(item1.Value, Is.EqualTo(clonedItem1.Value));
            Assert.That(item2.Value, Is.EqualTo(clonedItem2.Value));
            Assert.That(item3.Value, Is.EqualTo(clonedItem3.Value));

            Assert.That(container.Set1, Does.Contain(item1));
            Assert.That(container.Set2, Does.Contain(item2));
            Assert.That(container.Set3, Does.Contain(item3));

            Assert.That(clonedContainer.Set1, Does.Contain(clonedItem1));
            Assert.That(clonedContainer.Set2, Does.Contain(clonedItem2));
            Assert.That(clonedContainer.Set3, Does.Contain(clonedItem3));

            Assert.That(container.Set1, Has.Count.EqualTo(1));
            Assert.That(container.Set2, Has.Count.EqualTo(1));
            Assert.That(container.Set3, Has.Count.EqualTo(1));

            Assert.That(clonedContainer.Set1, Has.Count.EqualTo(1));
            Assert.That(clonedContainer.Set2, Has.Count.EqualTo(1));
            Assert.That(clonedContainer.Set3, Has.Count.EqualTo(1));
        });
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
    public void NestedDictionariesShouldBeClonedCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(outerDict, clonedOuterDict), Is.False, "Outer dictionaries should be different instances");
            Assert.That(ReferenceEquals(innerDict, clonedInnerDict), Is.False, "Inner dictionaries should be different instances");

            Assert.That(ReferenceEquals(key, clonedKey), Is.False, "Keys should be different instances");
            Assert.That(key.Value, Is.EqualTo(clonedKey.Value), "Key values should be equal");

            Assert.That(innerDict.ContainsKey(key), Is.True, "Original inner dict should contain original key");
            Assert.That(clonedInnerDict.ContainsKey(clonedKey), Is.True, "Cloned inner dict should contain cloned key");

            Assert.That(innerDict[key], Is.EqualTo("TestValue"), "Original value should be preserved");
            Assert.That(clonedInnerDict[clonedKey], Is.EqualTo("TestValue"), "Cloned value should be equal");

            Assert.That(outerDict.Keys, Has.Count.EqualTo(1), "Original outer dict should have one key");
            Assert.That(clonedOuterDict.Keys, Has.Count.EqualTo(1), "Cloned outer dict should have one key");
            Assert.That(innerDict.Keys, Has.Count.EqualTo(1), "Original inner dict should have one key");
            Assert.That(clonedInnerDict.Keys, Has.Count.EqualTo(1), "Cloned inner dict should have one key");
        });
    }


    [Test]
    public void SetBrokenAfterCloningTest()
    {
        // Arrange
        HashSet<KeyClass> originalSet = [];
        KeyClass key = new KeyClass { Value = "TestKey" };
        originalSet.Add(key);

        // Act
        HashSet<KeyClass> clonedSet = originalSet.DeepClone();
        KeyClass clonedKey = clonedSet.First();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(key, clonedKey), Is.False);
            Assert.That(key.Value, Is.EqualTo(clonedKey.Value));
            Assert.That(originalSet, Does.Contain(key));
            Assert.That(clonedSet, Does.Contain(clonedKey)); // important
            Assert.That(clonedKey.Value is "TestKey", Is.True);
        });
    }
    
    [Test]
    public void CanCopyTypes()
    {
        Type original = typeof(string);
        Type result = original.DeepClone();
        Assert.That(original, Is.EqualTo(result));
    }

    [Test]
    public void OrderedDictionaryBrokenAfterCloningTest()
    {
        // Arrange
        OrderedDictionary originalDict = new OrderedDictionary();
        KeyClass key = new KeyClass { Value = "TestKey" };
        originalDict[key] = "TestValue";

        // Act
        OrderedDictionary clonedDict = originalDict.DeepClone();
        KeyClass? clonedKey = clonedDict.Keys.Cast<KeyClass>().First();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(key, clonedKey), Is.False);
            Assert.That(key.Value, Is.EqualTo(clonedKey.Value));
            Assert.That(originalDict.Contains(key), Is.True);
            Assert.That(clonedDict.Contains(clonedKey), Is.True); // important
            Assert.That(clonedKey.Value is "TestKey", Is.True);
            Assert.That(clonedDict[clonedKey], Is.EqualTo("TestValue"));
        });
    }

    [Test]
    public void TaskCancelledExceptionCloningTest()
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
        Assert.Multiple(() =>
        {
            Assert.That(originalException, Is.Not.Null);
            
            Assert.DoesNotThrow(() =>
            {
                TaskCanceledException? clonedException = originalException.DeepClone();
                Assert.That(clonedException, Is.Not.Null);
                Assert.That(clonedException.Message, Is.EqualTo(originalException.Message));
                Assert.That(ReferenceEquals(originalException, clonedException), Is.False);
                Assert.That(clonedException.Task, Is.Not.Null);
            });
        });
        
        cts.Dispose();
    }

    [Test]
    public void SingleGenericDictionaryCloneTest()
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
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(key, clonedKey), Is.False);
            Assert.That(key.Value, Is.EqualTo(clonedKey.Value));

            Assert.That(originalDict.Count, Is.EqualTo(clonedDict.Count));
            Assert.That(clonedDict.Contains(clonedKvp), Is.True);

            Assert.That(clonedKey.Value, Is.EqualTo("TestKey"));
            Assert.That(clonedKvp.Value, Is.EqualTo("TestValue"));
        });
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
    [TestCase(false)]
    [TestCase(true)]
    public void Simple_Class_Should_Be_Cloned(bool isDeep)
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

        Assert.That(ReferenceEquals(cTo, cToRef), Is.True);
        Assert.That(cTo.A, Is.EqualTo(12));
        Assert.That(cTo.B, Is.EqualTo("testestest"));
        Assert.That(cTo.C.Length, Is.EqualTo(3));
        Assert.That(cTo.C[2], Is.EqualTo(3));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Descendant_Class_Should_Be_Cloned(bool isDeep)
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

        Assert.That(ReferenceEquals(cTo, cTo), Is.True);
        Assert.That(cTo.A, Is.EqualTo(11));
        Assert.That(((C1)cTo).A, Is.EqualTo(12));
        Assert.That(cTo.D, Is.EqualTo(42.3m));
    }

    [Test]
    public void Class_With_Subclass_Should_Be_Shallow_CLoned()
    {
        C1 c1 = new C1 { A = 12 };
        C3 cFrom = new C3 { A = c1, B = c1 };
        C3 cTo = cFrom.ShallowCloneTo(new C3());
        Assert.That(ReferenceEquals(cFrom.A, cTo.A), Is.True);
        Assert.That(ReferenceEquals(cFrom.B, cTo.B), Is.True);
        Assert.That(ReferenceEquals(cTo.A, cTo.B), Is.True);
    }

    [Test]
    public void Class_With_Subclass_Should_Be_Deep_CLoned()
    {
        C1 c1 = new C1 { A = 12 };
        C3 cFrom = new C3 { A = c1, B = c1 };
        C3 cTo = cFrom.DeepCloneTo(new C3());
        Assert.That(ReferenceEquals(cFrom.A, cTo.A), Is.False);
        Assert.That(ReferenceEquals(cFrom.B, cTo.B), Is.False);
        Assert.That(ReferenceEquals(cTo.A, cTo.B), Is.True);
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Copy_To_Null_Should_Return_Null(bool isDeep)
    {
        C1 c1 = new C1();
        if (isDeep)
            Assert.That(c1.DeepCloneTo((C1)null), Is.Null);
        else
            Assert.That(c1.ShallowCloneTo((C1)null), Is.Null);
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Copy_From_Null_Should_Throw_Error(bool isDeep)
    {
        C1 c1 = null;
        if (isDeep)
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<ArgumentNullException>(() => c1.DeepCloneTo(new C1()));
        else
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<ArgumentNullException>(() => c1.ShallowCloneTo(new C1()));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Invalid_Inheritance_Should_Throw_Error(bool isDeep)
    {
        C1 c1 = new C4();
        if (isDeep)
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<InvalidOperationException>(() => c1.DeepCloneTo(new C2()));
        else
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<InvalidOperationException>(() => c1.ShallowCloneTo(new C2()));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Struct_As_Interface_ShouldNot_Be_Cloned(bool isDeep)
    {
        S1 sFrom = new S1 { A = 42 };
        S1 sTo = new S1();
        I1? objTo = sTo;
        objTo.A = 23;
        if (isDeep)
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<InvalidOperationException>(() => ((I1)sFrom).DeepCloneTo(objTo));
        else
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<InvalidOperationException>(() => ((I1)sFrom).ShallowCloneTo(objTo));
    }

    [Test]
    public void String_Should_Not_Be_Cloned()
    {
        string s1 = "abc";
        string s2 = "def";
        Assert.Throws<InvalidOperationException>(() => s1.ShallowCloneTo(s2));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Array_Should_Be_Cloned_Correct_Size(bool isDeep)
    {
        int[] arrFrom = [1, 2, 3];
        int[] arrTo = [4, 5, 6];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        Assert.That(arrTo.Length, Is.EqualTo(3));
        Assert.That(arrTo[0], Is.EqualTo(1));
        Assert.That(arrTo[1], Is.EqualTo(2));
        Assert.That(arrTo[2], Is.EqualTo(3));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Array_Should_Be_Cloned_From_Is_Bigger(bool isDeep)
    {
        int[] arrFrom = [1, 2, 3];
        int[] arrTo = [4, 5];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        Assert.That(arrTo.Length, Is.EqualTo(2));
        Assert.That(arrTo[0], Is.EqualTo(1));
        Assert.That(arrTo[1], Is.EqualTo(2));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Array_Should_Be_Cloned_From_Is_Smaller(bool isDeep)
    {
        int[] arrFrom = [1, 2];
        int[] arrTo = [4, 5, 6];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        Assert.That(arrTo.Length, Is.EqualTo(3));
        Assert.That(arrTo[0], Is.EqualTo(1));
        Assert.That(arrTo[1], Is.EqualTo(2));
        Assert.That(arrTo[2], Is.EqualTo(6));
    }

    [Test]
    public void Shallow_Array_Should_Be_Cloned()
    {
        C1 c1 = new C1();
        C1[] arrFrom = [c1, c1, c1];
        C1[] arrTo = new C1[4];
        arrFrom.ShallowCloneTo(arrTo);
        Assert.That(arrTo.Length, Is.EqualTo(4));
        Assert.That(arrTo[0], Is.EqualTo(c1));
        Assert.That(arrTo[1], Is.EqualTo(c1));
        Assert.That(arrTo[2], Is.EqualTo(c1));
        Assert.That(arrTo[3], Is.Null);
    }

    [Test]
    public void Deep_Array_Should_Be_Cloned()
    {
        C4 c1 = new C4();
        C3 c3 = new C3 { A = c1, B = c1 };
        C3[] arrFrom = [c3, c3, c3];
        C3[] arrTo = new C3[4];
        arrFrom.DeepCloneTo(arrTo);
        Assert.That(arrTo.Length, Is.EqualTo(4));
#pragma warning disable NUnit2021 // Incompatible types for EqualTo constraint
        Assert.That(arrTo[0], Is.Not.EqualTo(c1));
#pragma warning restore NUnit2021 // Incompatible types for EqualTo constraint
        Assert.That(arrTo[0], Is.EqualTo(arrTo[1]));
        Assert.That(arrTo[1], Is.EqualTo(arrTo[2]));
        Assert.That(ReferenceEquals(arrTo[2].A, c1), Is.Not.True);
        Assert.That(arrTo[2].A, Is.EqualTo(arrTo[2].B));
        Assert.That(arrTo[3], Is.Null);
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Non_Zero_Based_Array_Should_Be_Cloned(bool isDeep)
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
        Assert.That(arrTo.Length, Is.EqualTo(2));
        Assert.That(arrTo.GetValue(0), Is.EqualTo(1));
        Assert.That(arrTo.GetValue(1), Is.EqualTo(2));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void MultiDim_Array_Should_Be_Cloned(bool isDeep)
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
        Assert.That(arrTo.Length, Is.EqualTo(1));
        Assert.That(arrTo.GetValue(0, 0), Is.EqualTo(1));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void TwoDim_Array_Should_Be_Cloned(bool isDeep)
    {
        int[,] arrFrom = { { 1, 2 }, { 3, 4 } };
        // with offset. its ok
        int[,] arrTo = new int[3, 1];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        Assert.That(arrTo[0, 0], Is.EqualTo(1));
        Assert.That(arrTo[1, 0], Is.EqualTo(3));

        arrTo = new int[2, 2];
        if (isDeep) arrFrom.DeepCloneTo(arrTo);
        else arrFrom.ShallowCloneTo(arrTo);
        Assert.That(arrTo[0, 0], Is.EqualTo(1));
        Assert.That(arrTo[0, 1], Is.EqualTo(2));
        Assert.That(arrTo[1, 0], Is.EqualTo(3));
    }

    [Test]
    public void MultiDim_Array_Should_Be_Cloned3()
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
        Assert.That(ReferenceEquals(arr, clone), Is.False);
        for (int i1 = 0; i1 < cnt1; i1++)
            for (int i2 = 0; i2 < cnt2; i2++)
                for (int i3 = 0; i3 < cnt3; i3++)
                    Assert.That(arr[i1, i2, i3], Is.EqualTo(i1 * 100 + i2 * 10 + i3));
    }

    [Test]
    public void MultiDimensional_Array_Should_Be_Cloned()
    {
        // Issue #25
        Array.CreateInstance(typeof(int), new[] { 0, 0 }).DeepCloneTo(new int[0, 0]);
        Array.CreateInstance(typeof(int), new[] { 1, 0 }).DeepCloneTo(new int[1, 0]);
        Array.CreateInstance(typeof(int), new[] { 0, 1 }).DeepCloneTo(new int[0, 1]);
        Array.CreateInstance(typeof(int), new[] { 1, 1 }).DeepCloneTo(new int[1, 1]);

        Array.CreateInstance(typeof(int), new[] { 0, 0, 0 }).DeepCloneTo(new int[0, 0, 0]);
        Array.CreateInstance(typeof(int), new[] { 1, 0, 0 }).DeepCloneTo(new int[1, 0, 0]);
        Array.CreateInstance(typeof(int), new[] { 0, 1, 0 }).DeepCloneTo(new int[0, 1, 0]);
        Array.CreateInstance(typeof(int), new[] { 0, 0, 1 }).DeepCloneTo(new int[0, 0, 1]);
        Array.CreateInstance(typeof(int), new[] { 1, 1, 1 }).DeepCloneTo(new int[1, 1, 1]);
    }

    [Test]
    public void Shallow_Clone_Of_MultiDim_Array_Should_Not_Perform_Deep()
    {
        C1 c1 = new C1();
        C1[,] arrFrom = { { c1, c1 }, { c1, c1 } };
        // with offset. its ok
        C1[,] arrTo = new C1[3, 1];
        arrFrom.ShallowCloneTo(arrTo);
        Assert.That(ReferenceEquals(c1, arrTo[0, 0]), Is.True);
        Assert.That(ReferenceEquals(c1, arrTo[1, 0]), Is.True);

        C1[,,] arrFrom2 = new C1[1, 1, 1];
        arrFrom2[0, 0, 0] = c1;
        C1[,,] arrTo2 = new C1[1, 1, 1];
        arrFrom2.ShallowCloneTo(arrTo2);
        Assert.That(ReferenceEquals(c1, arrTo2[0, 0, 0]), Is.True);
    }

    [Test]
    public void Deep_Clone_Of_MultiDim_Array_Should_Perform_Deep()
    {
        C1 c1 = new C1();
        C1[,] arrFrom = { { c1, c1 }, { c1, c1 } };
        // with offset. its ok
        C1[,] arrTo = new C1[3, 1];
        arrFrom.DeepCloneTo(arrTo);
        Assert.That(ReferenceEquals(c1, arrTo[0, 0]), Is.False);
        Assert.That(ReferenceEquals(arrTo[0, 0], arrTo[1, 0]), Is.True);

        C1[,,] arrFrom2 = new C1[1, 1, 2];
        arrFrom2[0, 0, 0] = c1;
        arrFrom2[0, 0, 1] = c1;
        C1[,,] arrTo2 = new C1[1, 1, 2];
        arrFrom2.DeepCloneTo(arrTo2);
        Assert.That(ReferenceEquals(c1, arrTo2[0, 0, 0]), Is.False);
        Assert.That(ReferenceEquals(arrTo2[0, 0, 1], arrTo2[0, 0, 0]), Is.True);
    }

    [Test]
    public void Dictionary_Should_Be_Deeply_Cloned()
    {
        Dictionary<string, string> d1 = new Dictionary<string, string>{ { "A", "B" }, { "C", "D" } };
        Dictionary<string, string> d2 = new Dictionary<string, string>();
        d1.DeepCloneTo(d2);
        d1["A"] = "E";
        Assert.That(d2.Count, Is.EqualTo(2));
        Assert.That(d2["A"], Is.EqualTo("B"));
        Assert.That(d2["C"], Is.EqualTo("D"));

        // big dictionary
        d1.Clear();
        for (int i = 0; i < 1000; i++)
            d1[i.ToString()] = i.ToString();
        d1.DeepCloneTo(d2);
        Assert.That(d2.Count, Is.EqualTo(1000));
        Assert.That(d2["557"], Is.EqualTo("557"));
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
    public void Inner_Implementation_In_Class_Should_Work()
    {
        D1 baseObject = new D1 { A = 12 };
        D2 wrapper = new D2(baseObject);
        Assert.That(wrapper.A, Is.EqualTo(12));
        Assert.That(wrapper.B, Is.EqualTo(14));
    }
    
    [Test]
    public void DictionaryWithStringKeys_FastPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(3));
            Assert.That(cloned["one"], Is.EqualTo(1));
            Assert.That(cloned["two"], Is.EqualTo(2));
            Assert.That(cloned["three"], Is.EqualTo(3));
            Assert.That(cloned.ContainsKey("one"), Is.True);
            Assert.That(cloned.ContainsKey("two"), Is.True);
            Assert.That(cloned.ContainsKey("three"), Is.True);
        });
    }
    
    [Test]
    public void DictionaryWithIntKeys_FastPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(3));
            Assert.That(cloned[1], Is.EqualTo("one"));
            Assert.That(cloned[2], Is.EqualTo("two"));
            Assert.That(cloned[100], Is.EqualTo("hundred"));
            Assert.That(cloned.ContainsKey(1), Is.True);
            Assert.That(cloned.ContainsKey(2), Is.True);
            Assert.That(cloned.ContainsKey(100), Is.True);
        });
    }
    
    [Test]
    public void DictionaryWithGuidKeys_FastPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(2));
            Assert.That(cloned[key1], Is.EqualTo("value1"));
            Assert.That(cloned[key2], Is.EqualTo("value2"));
            Assert.That(cloned.ContainsKey(key1), Is.True);
            Assert.That(cloned.ContainsKey(key2), Is.True);
        });
    }
    
    public record RecordKey(string Name, int Id);
    
    [Test]
    public void DictionaryWithRecordKeys_FastPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(2));
            
            // Records should be cloned (not same reference)
            Assert.That(clonedKey1, Is.Not.SameAs(key1));
            Assert.That(clonedKey2, Is.Not.SameAs(key2));
            
            // But should still be equal (value equality)
            Assert.That(clonedKey1, Is.EqualTo(key1));
            Assert.That(clonedKey2, Is.EqualTo(key2));
            
            // Dictionary lookups should work
            Assert.That(cloned.ContainsKey(clonedKey1), Is.True);
            Assert.That(cloned.ContainsKey(clonedKey2), Is.True);
            Assert.That(cloned[clonedKey1], Is.EqualTo("data1"));
            Assert.That(cloned[clonedKey2], Is.EqualTo("data2"));
        });
    }
    
    public struct StructKey
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    
    [Test]
    public void DictionaryWithStructKeys_FastPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(2));
            Assert.That(cloned.ContainsKey(key1), Is.True);
            Assert.That(cloned.ContainsKey(key2), Is.True);
            Assert.That(cloned[key1], Is.EqualTo("value1"));
            Assert.That(cloned[key2], Is.EqualTo("value2"));
        });
    }
    
    [Test]
    public void HashSetWithStrings_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - string elements have stable hash semantics (fast path)
        HashSet<string> original = ["apple", "banana", "cherry"];
        
        // Act
        HashSet<string> cloned = original.DeepClone();
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(3));
            Assert.That(cloned.Contains("apple"), Is.True);
            Assert.That(cloned.Contains("banana"), Is.True);
            Assert.That(cloned.Contains("cherry"), Is.True);
        });
    }
    
    [Test]
    public void HashSetWithInts_FastPath_ShouldCloneCorrectly()
    {
        // Arrange - int elements have stable hash semantics (fast path)
        HashSet<int> original = [1, 2, 3, 100, 999];
        
        // Act
        HashSet<int> cloned = original.DeepClone();
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(5));
            Assert.That(cloned.Contains(1), Is.True);
            Assert.That(cloned.Contains(2), Is.True);
            Assert.That(cloned.Contains(3), Is.True);
            Assert.That(cloned.Contains(100), Is.True);
            Assert.That(cloned.Contains(999), Is.True);
        });
    }
    
    [Test]
    public void HashSetWithRecords_FastPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(2));
            
            // Records should be cloned (not same reference)
            Assert.That(clonedItem1, Is.Not.SameAs(item1));
            Assert.That(clonedItem2, Is.Not.SameAs(item2));
            
            // But should still be equal (value equality)
            Assert.That(clonedItem1, Is.EqualTo(item1));
            Assert.That(clonedItem2, Is.EqualTo(item2));
            
            // Set lookups should work
            Assert.That(cloned.Contains(clonedItem1), Is.True);
            Assert.That(cloned.Contains(clonedItem2), Is.True);
        });
    }
    
    [Test]
    public void DictionaryWithReferenceKeys_SlowPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(2));
            
            // Keys should be cloned (not same reference)
            Assert.That(clonedKey1, Is.Not.SameAs(key1));
            Assert.That(clonedKey2, Is.Not.SameAs(key2));
            
            // Dictionary lookups should work with cloned keys (slow path ensures this)
            Assert.That(cloned.ContainsKey(clonedKey1), Is.True);
            Assert.That(cloned.ContainsKey(clonedKey2), Is.True);
            Assert.That(cloned[clonedKey1], Is.EqualTo(100));
            Assert.That(cloned[clonedKey2], Is.EqualTo(200));
        });
    }
    
    [Test]
    public void HashSetWithReferenceElements_SlowPath_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(2));
            
            // Elements should be cloned (not same reference)
            Assert.That(clonedItem1, Is.Not.SameAs(item1));
            Assert.That(clonedItem2, Is.Not.SameAs(item2));
            
            // Set lookups should work with cloned elements (slow path ensures this)
            Assert.That(cloned.Contains(clonedItem1), Is.True);
            Assert.That(cloned.Contains(clonedItem2), Is.True);
        });
    }
    
    [Test]
    public void LargeDictionary_FastPath_ShouldCloneEfficiently()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original));
            Assert.That(cloned.Count, Is.EqualTo(10000));
            
            // Verify some random entries
            Assert.That(cloned["key_0"], Is.EqualTo(0));
            Assert.That(cloned["key_5000"], Is.EqualTo(5000));
            Assert.That(cloned["key_9999"], Is.EqualTo(9999));
            Assert.That(cloned.ContainsKey("key_1234"), Is.True);
        });
    }
    
    [Test]
    public void LargeExpandoObject_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That((object)cloned, Is.Not.SameAs((object)original));
            Assert.That(clonedDict.Count, Is.EqualTo(100));
            
            // Verify string properties
            Assert.That(clonedDict["StringProp_0"], Is.EqualTo("String value 0"));
            Assert.That(clonedDict["StringProp_50"], Is.EqualTo("String value 50"));
            
            // Verify int properties
            Assert.That(clonedDict["IntProp_1"], Is.EqualTo(10));
            Assert.That(clonedDict["IntProp_51"], Is.EqualTo(510));
            
            // Verify double properties  
            Assert.That(clonedDict["DoubleProp_2"], Is.EqualTo(3.0));
            Assert.That(clonedDict["DoubleProp_52"], Is.EqualTo(78.0));
            
            // Verify nested anonymous types are cloned
            dynamic nested4 = clonedDict["NestedProp_4"];
            Assert.That((int)nested4.NestedId, Is.EqualTo(4));
            Assert.That((string)nested4.NestedValue, Is.EqualTo("Nested 4"));
            
            dynamic nested99 = clonedDict["NestedProp_99"];
            Assert.That((int)nested99.NestedId, Is.EqualTo(99));
            Assert.That((string)nested99.NestedValue, Is.EqualTo("Nested 99"));
        });
    }
    
    [Test]
    public void LargeExpandoObject_WithNestedExpandos_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That((object)cloned, Is.Not.SameAs((object)root));
            Assert.That((string)cloned.Name, Is.EqualTo("Root"));
            Assert.That((int)cloned.Level, Is.EqualTo(0));
            
            // Verify nested structure is cloned
            List<object> clonedChildren = cloned.Children;
            Assert.That(clonedChildren, Is.Not.SameAs((List<object>)root.Children));
            Assert.That(clonedChildren.Count, Is.EqualTo(1));
            
            dynamic clonedChild1 = clonedChildren[0];
            Assert.That((object)clonedChild1, Is.Not.SameAs((object)child1));
            Assert.That((string)clonedChild1.Name, Is.EqualTo("Child1"));
            
            dynamic clonedGrandchild = clonedChild1.Child;
            Assert.That((object)clonedGrandchild, Is.Not.SameAs((object)grandchild));
            Assert.That((string)clonedGrandchild.Name, Is.EqualTo("Grandchild"));
            
            string[] clonedTags = clonedGrandchild.Tags;
            Assert.That(clonedTags, Is.Not.SameAs((string[])grandchild.Tags));
            Assert.That(clonedTags, Is.EqualTo(new[] { "tag1", "tag2", "tag3" }));
        });
    }
    
    [Test]
    public void ExpandoObject_WithCircularReference_ShouldCloneCorrectly()
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
        Assert.Multiple(() =>
        {
            Assert.That((object)cloned, Is.Not.SameAs((object)parent));
            Assert.That((string)cloned.Name, Is.EqualTo("Parent"));
            Assert.That((int)cloned.Id, Is.EqualTo(1));
            
            // Verify circular reference is preserved
            dynamic clonedChild = cloned.Child;
            Assert.That((object)clonedChild, Is.Not.SameAs((object)child));
            Assert.That((string)clonedChild.Name, Is.EqualTo("Child"));
            
            // Child's parent should point to cloned parent, not original
            Assert.That((object)clonedChild.Parent, Is.SameAs((object)cloned));
            Assert.That((object)clonedChild.Parent, Is.Not.SameAs((object)parent));
            
            // Self reference should point to cloned parent
            Assert.That((object)cloned.Self, Is.SameAs((object)cloned));
            Assert.That((object)cloned.Self, Is.Not.SameAs((object)parent));
        });
    }
    
    [Test]
    public void LargeExpandoObject_BenchmarkScenario_VerifyDeepClone()
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
        
        Assert.Multiple(() =>
        {
            // 1. Clone is a different object instance
            Assert.That((object)cloned, Is.Not.SameAs((object)original), 
                "Clone must be a different object instance");
            
            // 2. All 100 properties are present
            Assert.That(clonedDict.Count, Is.EqualTo(100), 
                "Clone must have all 100 properties");
            
            // 3. Modifying clone does NOT affect original (independence test)
            clonedDict["StringProp_0"] = "MODIFIED";
            Assert.That(originalDict["StringProp_0"], Is.EqualTo("String value 0"), 
                "Modifying clone must not affect original string");
            
            clonedDict["IntProp_1"] = 99999;
            Assert.That(originalDict["IntProp_1"], Is.EqualTo(10), 
                "Modifying clone must not affect original int");
            
            // 4. Anonymous types are immutable, so sharing is correct - verify values match
            dynamic originalNested4 = originalDict["NestedProp_4"];
            dynamic clonedNested4 = clonedDict["NestedProp_4"];
            Assert.That((int)clonedNested4.NestedId, Is.EqualTo(4));
            Assert.That((string)clonedNested4.NestedValue, Is.EqualTo("Nested 4"));
            
            // 5. Verify multiple nested values are correct
            dynamic clonedNested99 = clonedDict["NestedProp_99"];
            Assert.That((int)clonedNested99.NestedId, Is.EqualTo(99));
            Assert.That((string)clonedNested99.NestedValue, Is.EqualTo("Nested 99"));
            
            // 6. Original values unchanged after all modifications
            Assert.That(originalDict["StringProp_50"], Is.EqualTo("String value 50"));
            Assert.That(originalDict["IntProp_51"], Is.EqualTo(510));
            Assert.That(originalDict["DoubleProp_52"], Is.EqualTo(78.0));
            
            // 7. Replacing nested property in clone doesn't affect original
            clonedDict["NestedProp_4"] = new { NestedId = 999, NestedValue = "Replaced" };
            Assert.That((int)originalNested4.NestedId, Is.EqualTo(4), 
                "Original nested value unchanged after replacing in clone");
        });
    }
    
    [Test]
    public void LargeExpandoObject_ModifyClonedNestedObjects_OriginalUnchanged()
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
        Assert.Multiple(() =>
        {
            // List modifications don't affect original
            List<string> originalList = original.MutableList;
            Assert.That(originalList.Count, Is.EqualTo(3), "Original list count unchanged");
            Assert.That(originalList[0], Is.EqualTo("Item1"), "Original list items unchanged");
            Assert.That(originalList.Contains("NewItem"), Is.False, "Original list doesn't have new item");
            
            // Dictionary modifications don't affect original
            Dictionary<string, int> originalDictionary = original.MutableDict;
            Assert.That(originalDictionary["Key1"], Is.EqualTo(1), "Original dict values unchanged");
            Assert.That(originalDictionary.ContainsKey("NewKey"), Is.False, "Original dict doesn't have new key");
            Assert.That(originalDictionary.Count, Is.EqualTo(2), "Original dict count unchanged");
            
            // Nested ExpandoObject modifications don't affect original
            Assert.That((string)original.NestedExpando.Value, Is.EqualTo("OriginalValue"), 
                "Original nested expando value unchanged");
            
            IDictionary<string, object?> originalNestedDict = original.NestedExpando;
            Assert.That(originalNestedDict.ContainsKey("NewProp"), Is.False, 
                "Original nested expando doesn't have new property");
            
            // Verify cloned values are actually modified
            Assert.That(cloned.MutableList.Count, Is.EqualTo(4));
            Assert.That(cloned.MutableDict["Key1"], Is.EqualTo(999));
            Assert.That((string)cloned.NestedExpando.Value, Is.EqualTo("ModifiedValue"));
        });
    }
}