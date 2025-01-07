using System.Collections;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DeepCloner.Core.Tests;

[TestFixture]
public class CopyToObjectSpec
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

        var to = source.DeepClone();
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
        var originalDict = new Dictionary<KeyClass, string>();
        var key = new KeyClass { Value = "TestKey" };
        originalDict[key] = "TestValue";

        // Act
        var clonedDict = originalDict.DeepClone();
        var clonedKey = clonedDict.Keys.First();

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
        var container = new DictionaryContainer
        {
            Dict1 = new Dictionary<KeyClass, string>(),
            Dict2 = new Dictionary<KeyClass, string>()
        };

        var key1 = new KeyClass { Value = "Key1" };
        var key2 = new KeyClass { Value = "Key2" };

        container.Dict1[key1] = "Value1";
        container.Dict2[key2] = "Value2";

        // Act
        var clonedContainer = container.DeepClone();
        var clonedKey1 = clonedContainer.Dict1.Keys.First();
        var clonedKey2 = clonedContainer.Dict2.Keys.First();

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
        var container = new HashSetContainer
        {
            Set1 = new HashSet<KeyClass>(),
            Set2 = new HashSet<KeyClass>(),
            Set3 = new HashSet<KeyClass>()
        };

        var item1 = new KeyClass { Value = "Item1" };
        var item2 = new KeyClass { Value = "Item2" };
        var item3 = new KeyClass { Value = "Item3" };

        container.Set1.Add(item1);
        container.Set2.Add(item2);
        container.Set3.Add(item3);

        // Act
        var clonedContainer = container.DeepClone();
        var clonedItem1 = clonedContainer.Set1.First();
        var clonedItem2 = clonedContainer.Set2.First();
        var clonedItem3 = clonedContainer.Set3.First();

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
        var outerDict = new Dictionary<string, Dictionary<KeyClass, string>>();
        var innerDict = new Dictionary<KeyClass, string>();
        var key = new KeyClass { Value = "TestKey" };
        innerDict[key] = "TestValue";
        outerDict["outer"] = innerDict;

        // Act
        var clonedOuterDict = outerDict.DeepClone();
        var clonedInnerDict = clonedOuterDict["outer"];
        var clonedKey = clonedInnerDict.Keys.First();

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
        var originalSet = new HashSet<KeyClass>();
        var key = new KeyClass { Value = "TestKey" };
        originalSet.Add(key);

        // Act
        var clonedSet = originalSet.DeepClone();
        var clonedKey = clonedSet.First();

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
    public void OrderedDictionaryBrokenAfterCloningTest()
    {
        // Arrange
        var originalDict = new OrderedDictionary();
        var key = new KeyClass { Value = "TestKey" };
        originalDict[key] = "TestValue";

        // Act
        var clonedDict = originalDict.DeepClone();
        var clonedKey = clonedDict.Keys.Cast<KeyClass>().First();

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
    public void SingleGenericDictionaryCloneTest()
    {
        // Arrange
        var originalDict = new SingleGenericDictionary<KeyValuePair<KeyClass, string>>();
        var key = new KeyClass { Value = "TestKey" };
        var kvp = new KeyValuePair<KeyClass, string>(key, "TestValue");
        originalDict.Add(kvp);

        // Act
        var clonedDict = originalDict.DeepClone();
        var clonedKvp = clonedDict.First();
        var clonedKey = clonedKvp.Key;

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(key, clonedKey), Is.False, "Klíč by měl být nová instance");
            Assert.That(key.Value, Is.EqualTo(clonedKey.Value), "Hodnota klíče by měla být zachována");

            Assert.That(originalDict.Count, Is.EqualTo(clonedDict.Count), "Počet položek by měl být stejný");
            Assert.That(clonedDict.Contains(clonedKvp), Is.True, "Naklonovaný slovník by měl obsahovat naklonovanou KeyValuePair");

            Assert.That(clonedKey.Value, Is.EqualTo("TestKey"), "Hodnota klíče by měla být zachována");
            Assert.That(clonedKvp.Value, Is.EqualTo("TestValue"), "Hodnota by měla být zachována");
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
        var cFrom = new C1
        {
            A = 12,
            B = "testestest",
            C = [1, 2, 3]
        };

        var cTo = new C1
        {
            A = 11,
            B = "tes",
            C = [1]
        };

        var cToRef = cTo;

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
        var cFrom = new C1
        {
            A = 12,
            B = "testestest",
            C = [1, 2, 3]
        };

        var cTo = new C2
        {
            A = 11,
            D = 42.3m
        };

        var cToRef = cTo;

        if (isDeep)
            cFrom.DeepCloneTo(cTo);
        else
            cFrom.ShallowCloneTo(cTo);

        Assert.That(ReferenceEquals(cTo, cToRef), Is.True);
        Assert.That(cTo.A, Is.EqualTo(11));
        Assert.That(((C1)cTo).A, Is.EqualTo(12));
        Assert.That(cTo.D, Is.EqualTo(42.3m));
    }

    [Test]
    public void Class_With_Subclass_Should_Be_Shallow_CLoned()
    {
        var c1 = new C1 { A = 12 };
        var cFrom = new C3 { A = c1, B = c1 };
        var cTo = cFrom.ShallowCloneTo(new C3());
        Assert.That(ReferenceEquals(cFrom.A, cTo.A), Is.True);
        Assert.That(ReferenceEquals(cFrom.B, cTo.B), Is.True);
        Assert.That(ReferenceEquals(cTo.A, cTo.B), Is.True);
    }

    [Test]
    public void Class_With_Subclass_Should_Be_Deep_CLoned()
    {
        var c1 = new C1 { A = 12 };
        var cFrom = new C3 { A = c1, B = c1 };
        var cTo = cFrom.DeepCloneTo(new C3());
        Assert.That(ReferenceEquals(cFrom.A, cTo.A), Is.False);
        Assert.That(ReferenceEquals(cFrom.B, cTo.B), Is.False);
        Assert.That(ReferenceEquals(cTo.A, cTo.B), Is.True);
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Copy_To_Null_Should_Return_Null(bool isDeep)
    {
        var c1 = new C1();
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
        var objTo = (I1)sTo;
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
        var s1 = "abc";
        var s2 = "def";
        Assert.Throws<InvalidOperationException>(() => s1.ShallowCloneTo(s2));
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void Array_Should_Be_Cloned_Correct_Size(bool isDeep)
    {
        var arrFrom = new[] { 1, 2, 3 };
        var arrTo = new[] { 4, 5, 6 };
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
        var arrFrom = new[] { 1, 2, 3 };
        var arrTo = new[] { 4, 5 };
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
        var arrFrom = new[] { 1, 2 };
        var arrTo = new[] { 4, 5, 6 };
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
        var c1 = new C1();
        var arrFrom = new[] { c1, c1, c1 };
        var arrTo = new C1[4];
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
        var c1 = new C4();
        var c3 = new C3 { A = c1, B = c1 };
        var arrFrom = new[] { c3, c3, c3 };
        var arrTo = new C3[4];
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
        var arrFrom = Array.CreateInstance(typeof(int),
            [2],
            [1]);
        // with offset. its ok
        var arrTo = Array.CreateInstance(typeof(int),
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
        var arrFrom = Array.CreateInstance(typeof(int),
            [2, 2],
            [1, 1]);
        // with offset. its ok
        var arrTo = Array.CreateInstance(typeof(int),
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
        var arrFrom = new[,] { { 1, 2 }, { 3, 4 } };
        // with offset. its ok
        var arrTo = new int[3, 1];
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
        var arr = new int[cnt1, cnt2, cnt3];
        for (var i1 = 0; i1 < cnt1; i1++)
            for (var i2 = 0; i2 < cnt2; i2++)
                for (var i3 = 0; i3 < cnt3; i3++)
                    arr[i1, i2, i3] = i1 * 100 + i2 * 10 + i3;
        var clone = arr.DeepCloneTo(new int[cnt1, cnt2, cnt3]);
        Assert.That(ReferenceEquals(arr, clone), Is.False);
        for (var i1 = 0; i1 < cnt1; i1++)
            for (var i2 = 0; i2 < cnt2; i2++)
                for (var i3 = 0; i3 < cnt3; i3++)
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
        var c1 = new C1();
        var arrFrom = new[,] { { c1, c1 }, { c1, c1 } };
        // with offset. its ok
        var arrTo = new C1[3, 1];
        arrFrom.ShallowCloneTo(arrTo);
        Assert.That(ReferenceEquals(c1, arrTo[0, 0]), Is.True);
        Assert.That(ReferenceEquals(c1, arrTo[1, 0]), Is.True);

        var arrFrom2 = new C1[1, 1, 1];
        arrFrom2[0, 0, 0] = c1;
        var arrTo2 = new C1[1, 1, 1];
        arrFrom2.ShallowCloneTo(arrTo2);
        Assert.That(ReferenceEquals(c1, arrTo2[0, 0, 0]), Is.True);
    }

    [Test]
    public void Deep_Clone_Of_MultiDim_Array_Should_Perform_Deep()
    {
        var c1 = new C1();
        var arrFrom = new[,] { { c1, c1 }, { c1, c1 } };
        // with offset. its ok
        var arrTo = new C1[3, 1];
        arrFrom.DeepCloneTo(arrTo);
        Assert.That(ReferenceEquals(c1, arrTo[0, 0]), Is.False);
        Assert.That(ReferenceEquals(arrTo[0, 0], arrTo[1, 0]), Is.True);

        var arrFrom2 = new C1[1, 1, 2];
        arrFrom2[0, 0, 0] = c1;
        arrFrom2[0, 0, 1] = c1;
        var arrTo2 = new C1[1, 1, 2];
        arrFrom2.DeepCloneTo(arrTo2);
        Assert.That(ReferenceEquals(c1, arrTo2[0, 0, 0]), Is.False);
        Assert.That(ReferenceEquals(arrTo2[0, 0, 1], arrTo2[0, 0, 0]), Is.True);
    }

    [Test]
    public void Dictionary_Should_Be_Deeply_Cloned()
    {
        var d1 = new Dictionary<string, string>{ { "A", "B" }, { "C", "D" } };
        var d2 = new Dictionary<string, string>();
        d1.DeepCloneTo(d2);
        d1["A"] = "E";
        Assert.That(d2.Count, Is.EqualTo(2));
        Assert.That(d2["A"], Is.EqualTo("B"));
        Assert.That(d2["C"], Is.EqualTo("D"));

        // big dictionary
        d1.Clear();
        for (var i = 0; i < 1000; i++)
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
        var baseObject = new D1 { A = 12 };
        var wrapper = new D2(baseObject);
        Assert.That(wrapper.A, Is.EqualTo(12));
        Assert.That(wrapper.B, Is.EqualTo(14));
    }
}