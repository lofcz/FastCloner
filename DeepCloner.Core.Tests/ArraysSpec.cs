using NUnit.Framework.Legacy;
using System.Text;

namespace DeepCloner.Core.Tests;

[TestFixture]
public class ArraysSpec
{
    [Test]
    public void IntArray_Should_Be_Cloned()
    {
        int[] arr = [1, 2, 3];
        int[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(3));
        CollectionAssert.AreEqual(arr, cloned);
    }

    [Test]
    public void StringArray_Should_Be_Cloned()
    {
        string[] arr = ["1", "2", "3"];
        string[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(3));
        CollectionAssert.AreEqual(arr, cloned);
    }

    [Test]
    public void StringArray_Should_Be_Cloned_Two_Arrays()
    {
        // checking that cached object correctly clones arrays of different length
        string[]? arr = ["111111111111111111111", "2", "3"];
        string[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(3));
        CollectionAssert.AreEqual(arr, cloned);
        // strings should not be copied
        Assert.That(ReferenceEquals(arr[1], cloned[1]), Is.True);

        arr = ["1", "2", "3", "4"];
        cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(4));
        CollectionAssert.AreEqual(arr, cloned);

        arr = [];
        cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(0));

        if (1.Equals(1)) arr = null;
        Assert.That(arr.DeepClone(), Is.Null);
    }

    [Test]
    public void StringArray_Casted_As_Object_Should_Be_Cloned()
    {
        // checking that cached object correctly clones arrays of different length
        object arr = new[] { "1", "2", "3" };
        string[]? cloned = arr.DeepClone() as string[];
        Assert.That(cloned.Length, Is.EqualTo(3));
        CollectionAssert.AreEqual((string[])arr, cloned);
        // strings should not be copied
        Assert.That(ReferenceEquals(((string[])arr)[1], cloned[1]), Is.True);
    }

    [Test]
    public void ByteArray_Should_Be_Cloned()
    {
        // checking that cached object correctly clones arrays of different length
        byte[] arr = Encoding.ASCII.GetBytes("test");
        byte[] cloned = arr.DeepClone();
        CollectionAssert.AreEqual(arr, cloned);

        arr = Encoding.ASCII.GetBytes("test testtest testtest testtest testtest testtest testtest testtest testtest testtest testtest testtest testtest testte");
        cloned = arr.DeepClone();
        CollectionAssert.AreEqual(arr, cloned);
    }

    public class C1
    {
        public C1(int x) => X = x;

        public int X { get; set; }

        public Guid Q { get; } = Guid.NewGuid();
    }

    [Test]
    public void ClassArray_Should_Be_Cloned()
    {
        C1[] arr = [new C1(1), new C1(2)];
        C1[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(2));
        Assert.That(cloned[0].X, Is.EqualTo(1));
        Assert.That(cloned[1].X, Is.EqualTo(2));
        Assert.That(ReferenceEquals(cloned[0], arr[0]), Is.Not.True);
        Assert.That(ReferenceEquals(cloned[1], arr[1]), Is.Not.True);
    }

    public struct S1
    {
        public S1(int x) => X = x;

        public int X;
    }

    public struct S2
    {
        public C1 C;
    }

    [Test]
    public void StructArray_Should_Be_Cloned()
    {
        S1[] arr = [new S1(1), new S1(2)];
        S1[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(2));
        Assert.That(cloned[0].X, Is.EqualTo(1));
        Assert.That(cloned[1].X, Is.EqualTo(2));
    }

    [Test]
    public void StructArray_With_Class_Should_Be_Cloned()
    {
        S2[] arr = [new S2 { C = new C1(1) }, new S2 { C = new C1(2) }];
        S2[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(2));
        Assert.That(cloned[0].C.X, Is.EqualTo(1));
        Assert.That(cloned[1].C.X, Is.EqualTo(2));
        Assert.That(ReferenceEquals(cloned[0].C, arr[0].C), Is.Not.True);
        Assert.That(ReferenceEquals(cloned[1].C, arr[1].C), Is.Not.True);
    }

    [Test]
    public void NullArray_hould_Be_Cloned()
    {
        C1[] arr = [null, null];
        C1[] cloned = arr.DeepClone();
        Assert.That(cloned.Length, Is.EqualTo(2));
        Assert.That(cloned[0], Is.Null);
        Assert.That(cloned[1], Is.Null);
    }

    [Test]
    public void NullAsArray_hould_Be_Cloned()
    {
        int[]? arr = null;
// ReSharper disable ExpressionIsAlwaysNull
        int[]? cloned = arr.DeepClone();
// ReSharper restore ExpressionIsAlwaysNull
        Assert.That(cloned, Is.Null);
    }

    [Test]
    public void IntList_Should_Be_Cloned()
    {
        // TODO: better performance for this type
        List<int> arr = [1, 2, 3];
        List<int> cloned = arr.DeepClone();
        Assert.That(cloned.Count, Is.EqualTo(3));
        Assert.That(cloned[0], Is.EqualTo(1));
        Assert.That(cloned[1], Is.EqualTo(2));
        Assert.That(cloned[2], Is.EqualTo(3));
    }

    [Test]
    public void Dictionary_Should_Be_Cloned()
    {
        // TODO: better performance for this type
        Dictionary<string, decimal> d = new Dictionary<string, decimal>();
        d["a"] = 1;
        d["b"] = 2;
        Dictionary<string, decimal> cloned = d.DeepClone();
        Assert.That(cloned.Count, Is.EqualTo(2));
        Assert.That(cloned["a"], Is.EqualTo(1));
        Assert.That(cloned["b"], Is.EqualTo(2));
    }

    [Test]
    public void Array_Of_Same_Arrays_Should_Be_Cloned()
    {
        int[] c1 = [1, 2, 3];
        int[][] arr = [c1, c1, c1, c1, c1];
        int[][] cloned = arr.DeepClone();

        Assert.That(cloned.Length, Is.EqualTo(5));
        // lot of objects for checking reference dictionary optimization
        Assert.That(ReferenceEquals(arr[0], cloned[0]), Is.False);
        Assert.That(ReferenceEquals(cloned[0], cloned[1]), Is.True);
        Assert.That(ReferenceEquals(cloned[1], cloned[2]), Is.True);
        Assert.That(ReferenceEquals(cloned[1], cloned[3]), Is.True);
        Assert.That(ReferenceEquals(cloned[1], cloned[4]), Is.True);
    }

    public class AC
    {
        public int[] A { get; set; }

        public int[] B { get; set; }
    }

    [Test]
    public void Class_With_Same_Arrays_Should_Be_Cloned()
    {
        AC ac = new AC();
        ac.A = ac.B = new int[3];
        AC clone = ac.DeepClone();
        Assert.That(ReferenceEquals(ac.A, clone.A), Is.False);
        Assert.That(ReferenceEquals(clone.A, clone.B), Is.True);
    }

    [Test]
    public void Class_With_Null_Array_hould_Be_Cloned()
    {
        AC ac = new AC();
        AC cloned = ac.DeepClone();
        Assert.That(cloned.A, Is.Null);
        Assert.That(cloned.B, Is.Null);
    }

    [Test]
    public void MultiDim_Array_Should_Be_Cloned()
    {
        int[,] arr = new int[2, 2];
        arr[0, 0] = 1;
        arr[0, 1] = 2;
        arr[1, 0] = 3;
        arr[1, 1] = 4;
        int[,] clone = arr.DeepClone();
        Assert.That(ReferenceEquals(arr, clone), Is.False);
        Assert.That(clone[0, 0], Is.EqualTo(1));
        Assert.That(clone[0, 1], Is.EqualTo(2));
        Assert.That(clone[1, 0], Is.EqualTo(3));
        Assert.That(clone[1, 1], Is.EqualTo(4));
    }

    [Test]
    public void MultiDim_Array_Should_Be_Cloned2()
    {
        int[,,] arr = new int[2, 2, 1];
        arr[0, 0, 0] = 1;
        arr[0, 1, 0] = 2;
        arr[1, 0, 0] = 3;
        arr[1, 1, 0] = 4;
        int[,,] clone = arr.DeepClone();
        Assert.That(ReferenceEquals(arr, clone), Is.False);
        Assert.That(clone[0, 0, 0], Is.EqualTo(1));
        Assert.That(clone[0, 1, 0], Is.EqualTo(2));
        Assert.That(clone[1, 0, 0], Is.EqualTo(3));
        Assert.That(clone[1, 1, 0], Is.EqualTo(4));
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
        int[,,] clone = arr.DeepClone();
        Assert.That(ReferenceEquals(arr, clone), Is.False);
        for (int i1 = 0; i1 < cnt1; i1++)
            for (int i2 = 0; i2 < cnt2; i2++)
                for (int i3 = 0; i3 < cnt3; i3++)
                    Assert.That(arr[i1, i2, i3], Is.EqualTo(i1 * 100 + i2 * 10 + i3));
    }

    [Test]
    public void MultiDim_Array_Of_Classes_Should_Be_Cloned()
    {
        AC[,] arr = new AC[2, 2];
        arr[0, 0] = arr[1, 1] = new AC();
        AC[,] clone = arr.DeepClone();
        Assert.That(clone[0, 0], Is.Not.Null);
        Assert.That(clone[1, 1], Is.Not.Null);
        Assert.That(ReferenceEquals(clone[1, 1], clone[0, 0]));
        Assert.That(ReferenceEquals(clone[1, 1], arr[0, 0]), Is.Not.True);
    }

    [Test]
    public void NonZero_Based_Array_Should_Be_Cloned()
    {
        Array arr = Array.CreateInstance(typeof(int),
            [2],
            [1]);

        arr.SetValue(1, 1);
        arr.SetValue(2, 2);
        Array clone = arr.DeepClone();
        Assert.That(clone.GetValue(1), Is.EqualTo(1));
        Assert.That(clone.GetValue(2), Is.EqualTo(2));
    }

    [Test]
    public void NonZero_Based_MultiDim_Array_Should_Be_Cloned()
    {
        Array arr = Array.CreateInstance(typeof(int),
            [2, 2],
            [1, 1]);

        arr.SetValue(1, 1, 1);
        arr.SetValue(2, 2, 2);
        Array clone = arr.DeepClone();
        Assert.That(clone.GetValue(1, 1), Is.EqualTo(1));
        Assert.That(clone.GetValue(2, 2), Is.EqualTo(2));
    }

    [Test]
    public void Array_As_Generic_Array_Should_Be_Cloned()
    {
        int[] arr = [1, 2, 3];
        Array genArr = arr;
        int[] clone = (int[])genArr.DeepClone();
        Assert.That(clone.Length, Is.EqualTo(3));
        Assert.That(clone[0], Is.EqualTo(1));
        Assert.That(clone[1], Is.EqualTo(2));
        Assert.That(clone[2], Is.EqualTo(3));
    }

    [Test]
    public void Array_As_IEnumerable_Should_Be_Cloned()
    {
        int[] arr = [1, 2, 3];
        IEnumerable<int> genArr = arr;
        int[] clone = (int[])genArr.DeepClone();
// ReSharper disable PossibleMultipleEnumeration
        Assert.That(clone.Length, Is.EqualTo(3));
        Assert.That(clone[0], Is.EqualTo(1));
        Assert.That(clone[1], Is.EqualTo(2));
        Assert.That(clone[2], Is.EqualTo(3));
        // ReSharper restore PossibleMultipleEnumeration
    }

    [Test]
    public void MultiDimensional_Array_Should_Be_Cloned()
    {
        // Issue #25
        Array.CreateInstance(typeof(int), new[] { 0, 0 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 1, 0 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 0, 1 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 1, 1 }).DeepClone();

        Array.CreateInstance(typeof(int), new[] { 0, 0, 0 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 1, 0, 0 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 0, 1, 0 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 0, 0, 1 }).DeepClone();
        Array.CreateInstance(typeof(int), new[] { 1, 1, 1 }).DeepClone();
    }

    [Test]
    public void Issue_17_Spec()
    {
        HashSet<string> set = ["value"];
        Assert.That(set.Contains("value"), Is.True);

        HashSet<string> cloned = set.DeepClone();
        Assert.That(cloned.Contains("value"), Is.True);

        HashSet<string> copyOfSet = new HashSet<string>(set, set.Comparer);
        Assert.That(copyOfSet.Contains("value"), Is.True);

        HashSet<string> copyOfCloned = new HashSet<string>(cloned, cloned.Comparer);
        Assert.That(copyOfCloned.ToArray()[0] == "value", Is.True);

        Assert.That(copyOfCloned.Contains("value"), Is.True);
    }

    [Test]
    public void Check_Comparer_does_not_Clone()
    {
        Check_Comparer_does_not_Clone_Internal<string>();
        Check_Comparer_does_not_Clone_Internal<int>();
        Check_Comparer_does_not_Clone_Internal<object>();
        Check_Comparer_does_not_Clone_Internal<FileShare>();
        Check_Comparer_does_not_Clone_Internal<byte[]>();
        Check_Comparer_does_not_Clone_Internal<byte>();
        Check_Comparer_does_not_Clone_Internal<int?>();
        Check_Comparer_does_not_Clone_Internal<HashSet<int>>();
        Assert.That(StringComparer.Ordinal == StringComparer.Ordinal.DeepClone(), Is.True);
        Assert.That(StringComparer.OrdinalIgnoreCase == StringComparer.OrdinalIgnoreCase.DeepClone(), Is.True);
        Assert.That(StringComparer.InvariantCulture == StringComparer.InvariantCulture.DeepClone(), Is.True);
        Assert.That(StringComparer.InvariantCultureIgnoreCase == StringComparer.InvariantCultureIgnoreCase.DeepClone(), Is.True);
    }

    private void Check_Comparer_does_not_Clone_Internal<T>()
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        EqualityComparer<T> cloned = comparer.DeepClone();

        // checking by reference
        Assert.That(comparer == cloned, Is.True);
    }
}