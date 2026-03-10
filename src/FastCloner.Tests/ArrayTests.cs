using System.Text;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class ArrayTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    struct MyIntStruct
    {
        public int val;
    }
    
    [Test]
    public void Array2dPerf()
    {
        const int SIZE = 100;
        
        MyIntStruct[,] testData = new MyIntStruct[SIZE, SIZE];
        
        for (int i = 0; i < SIZE; i++)
        {
            for (int j = 0; j < SIZE; j++)
            {
                testData[i, j] = new MyIntStruct
                {
                    val = i * j
                };
            }
        }

        testData.DeepClone();
    }
    
    [Test]
    public async Task IntArray_Should_Be_Cloned()
    {
        int[] arr = [1, 2, 3];
        int[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(3);
        await Assert.That(cloned).IsEquivalentTo(arr);
    }

    [Test]
    public async Task StringArray_Should_Be_Cloned()
    {
        string[] arr = ["1", "2", "3"];
        string[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(3);
        await Assert.That(cloned).IsEquivalentTo(arr);
    }

    [Test]
    public async Task StringArray_Should_Be_Cloned_Two_Arrays()
    {
        // checking that cached object correctly clones arrays of different length
        string[]? arr = ["111111111111111111111", "2", "3"];
        string[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(3);
        await Assert.That(cloned).IsEquivalentTo(arr);
        // strings should not be copied
        await Assert.That(ReferenceEquals(arr[1], cloned[1])).IsTrue();

        arr = ["1", "2", "3", "4"];
        cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(4);
        await Assert.That(cloned).IsEquivalentTo(arr);

        arr = [];
        cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(0);

        if (1.Equals(1)) arr = null;
        await Assert.That(arr.DeepClone()).IsNull();
    }

    [Test]
    public async Task StringArray_Casted_As_Object_Should_Be_Cloned()
    {
        // checking that cached object correctly clones arrays of different length
        object arr = new[] { "1", "2", "3" };
        string[]? cloned = arr.DeepClone() as string[];
        await Assert.That(cloned.Length).IsEqualTo(3);
        await Assert.That(cloned).IsEquivalentTo((string[])arr);
        // strings should not be copied
        await Assert.That(ReferenceEquals(((string[])arr)[1], cloned[1])).IsTrue();
    }

    [Test]
    public async Task ByteArray_Should_Be_Cloned()
    {
        // checking that cached object correctly clones arrays of different length
        byte[] arr = "test"u8.ToArray();
        byte[] cloned = arr.DeepClone();
        await Assert.That(cloned).IsEquivalentTo(arr);

        arr = "test testtest testtest testtest testtest testtest testtest testtest testtest testtest testtest testtest testtest testte"u8.ToArray();
        cloned = arr.DeepClone();
        await Assert.That(cloned).IsEquivalentTo(arr);
    }

    public class C1
    {
        public C1(int x) => X = x;

        public int X { get; set; }

        public Guid Q { get; } = Guid.NewGuid();
    }

    [Test]
    public async Task ClassArray_Should_Be_Cloned()
    {
        C1[] arr = [new C1(1), new C1(2)];
        C1[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(2);
        await Assert.That(cloned[0].X).IsEqualTo(1);
        await Assert.That(cloned[1].X).IsEqualTo(2);
        await Assert.That(ReferenceEquals(cloned[0], arr[0])).IsNotEqualTo(true);
        await Assert.That(ReferenceEquals(cloned[1], arr[1])).IsNotEqualTo(true);
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
    public async Task StructArray_Should_Be_Cloned()
    {
        S1[] arr = [new S1(1), new S1(2)];
        S1[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(2);
        await Assert.That(cloned[0].X).IsEqualTo(1);
        await Assert.That(cloned[1].X).IsEqualTo(2);
    }

    [Test]
    public async Task StructArray_With_Class_Should_Be_Cloned()
    {
        S2[] arr = [new S2 { C = new C1(1) }, new S2 { C = new C1(2) }];
        S2[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(2);
        await Assert.That(cloned[0].C.X).IsEqualTo(1);
        await Assert.That(cloned[1].C.X).IsEqualTo(2);
        await Assert.That(ReferenceEquals(cloned[0].C, arr[0].C)).IsNotEqualTo(true);
        await Assert.That(ReferenceEquals(cloned[1].C, arr[1].C)).IsNotEqualTo(true);
    }

    [Test]
    public async Task NullArray_hould_Be_Cloned()
    {
        C1[] arr = [null, null];
        C1[] cloned = arr.DeepClone();
        await Assert.That(cloned.Length).IsEqualTo(2);
        await Assert.That(cloned[0]).IsNull();
        await Assert.That(cloned[1]).IsNull();
    }

    [Test]
    public async Task NullAsArray_hould_Be_Cloned()
    {
        int[]? arr = null;
// ReSharper disable ExpressionIsAlwaysNull
        int[]? cloned = arr.DeepClone();
// ReSharper restore ExpressionIsAlwaysNull
        await Assert.That(cloned).IsNull();
    }

    [Test]
    public async Task IntList_Should_Be_Cloned()
    {
        List<int> arr = [1, 2, 3];
        List<int> cloned = arr.DeepClone();
        await Assert.That(cloned.Count).IsEqualTo(3);
        await Assert.That(cloned[0]).IsEqualTo(1);
        await Assert.That(cloned[1]).IsEqualTo(2);
        await Assert.That(cloned[2]).IsEqualTo(3);
    }

    [Test]
    public async Task Dictionary_Should_Be_Cloned()
    {
        Dictionary<string, decimal> d = new Dictionary<string, decimal>
        {
            ["a"] = 1,
            ["b"] = 2
        };
        Dictionary<string, decimal> cloned = d.DeepClone();
        await Assert.That(cloned.Count).IsEqualTo(2);
        await Assert.That(cloned["a"]).IsEqualTo(1);
        await Assert.That(cloned["b"]).IsEqualTo(2);
    }

    [Test]
    public async Task Array_Of_Same_Arrays_Should_Be_Cloned()
    {
        int[] c1 = [1, 2, 3];
        int[][] arr = [c1, c1, c1, c1, c1];
        int[][] cloned = arr.DeepClone();

        await Assert.That(cloned.Length).IsEqualTo(5);
        // lot of objects for checking reference dictionary optimization
        await Assert.That(ReferenceEquals(arr[0], cloned[0])).IsFalse();
        await Assert.That(ReferenceEquals(cloned[0], cloned[1])).IsTrue();
        await Assert.That(ReferenceEquals(cloned[1], cloned[2])).IsTrue();
        await Assert.That(ReferenceEquals(cloned[1], cloned[3])).IsTrue();
        await Assert.That(ReferenceEquals(cloned[1], cloned[4])).IsTrue();
    }

    public class Ac
    {
        public int[] A { get; set; } = null!;

        public int[] B { get; set; } = null!;
    }

    [Test]
    public async Task Class_With_Same_Arrays_Should_Be_Cloned()
    {
        Ac ac = new Ac();
        ac.A = ac.B = new int[3];
        Ac clone = ac.DeepClone();
        await Assert.That(ReferenceEquals(ac.A, clone.A)).IsFalse();
        await Assert.That(ReferenceEquals(clone.A, clone.B)).IsTrue();
    }

    [Test]
    public async Task Class_With_Null_Array_hould_Be_Cloned()
    {
        Ac ac = new Ac();
        Ac cloned = ac.DeepClone();
        await Assert.That(cloned.A).IsNull();
        await Assert.That(cloned.B).IsNull();
    }

    [Test]
    public async Task MultiDim_Array_Should_Be_Cloned()
    {
        int[,] arr = new int[2, 2];
        arr[0, 0] = 1;
        arr[0, 1] = 2;
        arr[1, 0] = 3;
        arr[1, 1] = 4;
        int[,] clone = arr.DeepClone();
        await Assert.That(ReferenceEquals(arr, clone)).IsFalse();
        await Assert.That(clone[0, 0]).IsEqualTo(1);
        await Assert.That(clone[0, 1]).IsEqualTo(2);
        await Assert.That(clone[1, 0]).IsEqualTo(3);
        await Assert.That(clone[1, 1]).IsEqualTo(4);
    }

    [Test]
    public async Task MultiDim_Array_Should_Be_Cloned2()
    {
        int[,,] arr = new int[2, 2, 1];
        arr[0, 0, 0] = 1;
        arr[0, 1, 0] = 2;
        arr[1, 0, 0] = 3;
        arr[1, 1, 0] = 4;
        int[,,] clone = arr.DeepClone();
        await Assert.That(ReferenceEquals(arr, clone)).IsFalse();
        await Assert.That(clone[0, 0, 0]).IsEqualTo(1);
        await Assert.That(clone[0, 1, 0]).IsEqualTo(2);
        await Assert.That(clone[1, 0, 0]).IsEqualTo(3);
        await Assert.That(clone[1, 1, 0]).IsEqualTo(4);
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
        int[,,] clone = arr.DeepClone();
        await Assert.That(ReferenceEquals(arr, clone)).IsFalse();
        for (int i1 = 0; i1 < cnt1; i1++)
            for (int i2 = 0; i2 < cnt2; i2++)
                for (int i3 = 0; i3 < cnt3; i3++)
                    await Assert.That(arr[i1, i2, i3]).IsEqualTo(i1 * 100 + i2 * 10 + i3);
    }

    [Test]
    public async Task MultiDim_Array_Of_Classes_Should_Be_Cloned()
    {
        Ac[,] arr = new Ac[2, 2];
        arr[0, 0] = arr[1, 1] = new Ac();
        Ac[,] clone = arr.DeepClone();
        await Assert.That(clone[0, 0]).IsNotNull();
        await Assert.That(clone[1, 1]).IsNotNull();
        await Assert.That(ReferenceEquals(clone[1, 1], clone[0, 0])).IsTrue();
        await Assert.That(ReferenceEquals(clone[1, 1], arr[0, 0])).IsFalse();
    }

    [Test]
    public async Task NonZero_Based_Array_Should_Be_Cloned()
    {
        Array arr = Array.CreateInstance(typeof(int),
            [2],
            [1]);

        arr.SetValue(1, 1);
        arr.SetValue(2, 2);
        Array clone = arr.DeepClone();
        await Assert.That(clone.GetValue(1)).IsEqualTo(1);
        await Assert.That(clone.GetValue(2)).IsEqualTo(2);
    }

    [Test]
    public async Task NonZero_Based_MultiDim_Array_Should_Be_Cloned()
    {
        Array arr = Array.CreateInstance(typeof(int),
            [2, 2],
            [1, 1]);

        arr.SetValue(1, 1, 1);
        arr.SetValue(2, 2, 2);
        Array clone = arr.DeepClone();
        await Assert.That(clone.GetValue(1, 1)).IsEqualTo(1);
        await Assert.That(clone.GetValue(2, 2)).IsEqualTo(2);
    }

    [Test]
    public async Task Array_As_Generic_Array_Should_Be_Cloned()
    {
        int[] arr = [1, 2, 3];
        Array genArr = arr;
        int[] clone = (int[])genArr.DeepClone();
        await Assert.That(clone.Length).IsEqualTo(3);
        await Assert.That(clone[0]).IsEqualTo(1);
        await Assert.That(clone[1]).IsEqualTo(2);
        await Assert.That(clone[2]).IsEqualTo(3);
    }

    [Test]
    public async Task Array_As_IEnumerable_Should_Be_Cloned()
    {
        int[] arr = [1, 2, 3];
        IEnumerable<int> genArr = arr;
        int[] clone = (int[])genArr.DeepClone();
        await Assert.That(clone.Length).IsEqualTo(3);
        await Assert.That(clone[0]).IsEqualTo(1);
        await Assert.That(clone[1]).IsEqualTo(2);
        await Assert.That(clone[2]).IsEqualTo(3);
    }

    [Test]
    public void MultiDimensional_Array_Should_Be_Cloned()
    {
        // Issue #25
        Array.CreateInstance(typeof(int), [0, 0]).DeepClone();
        Array.CreateInstance(typeof(int), [1, 0]).DeepClone();
        Array.CreateInstance(typeof(int), [0, 1]).DeepClone();
        Array.CreateInstance(typeof(int), [1, 1]).DeepClone();

        Array.CreateInstance(typeof(int), [0, 0, 0]).DeepClone();
        Array.CreateInstance(typeof(int), [1, 0, 0]).DeepClone();
        Array.CreateInstance(typeof(int), [0, 1, 0]).DeepClone();
        Array.CreateInstance(typeof(int), [0, 0, 1]).DeepClone();
        Array.CreateInstance(typeof(int), [1, 1, 1]).DeepClone();
    }

    [Test]
    public async Task Issue_17_Spec()
    {
        HashSet<string> set = ["value"];
        await Assert.That(set.Contains("value")).IsTrue();

        HashSet<string> cloned = set.DeepClone();
        await Assert.That(cloned.Contains("value")).IsTrue();

        HashSet<string> copyOfSet = new HashSet<string>(set, set.Comparer);
        await Assert.That(copyOfSet.Contains("value")).IsTrue();

        HashSet<string> copyOfCloned = new HashSet<string>(cloned, cloned.Comparer);
        await Assert.That(copyOfCloned.ToArray()[0] == "value").IsTrue();

        await Assert.That(copyOfCloned.Contains("value")).IsTrue();
    }

    [Test]
    public async Task Check_Comparer_does_not_Clone()
    {
        Check_Comparer_does_not_Clone_Internal<string>();
        Check_Comparer_does_not_Clone_Internal<int>();
        Check_Comparer_does_not_Clone_Internal<object>();
        Check_Comparer_does_not_Clone_Internal<FileShare>();
        Check_Comparer_does_not_Clone_Internal<byte[]>();
        Check_Comparer_does_not_Clone_Internal<byte>();
        Check_Comparer_does_not_Clone_Internal<int?>();
        Check_Comparer_does_not_Clone_Internal<HashSet<int>>();
        await Assert.That(StringComparer.Ordinal == StringComparer.Ordinal.DeepClone()).IsTrue();
        await Assert.That(StringComparer.OrdinalIgnoreCase == StringComparer.OrdinalIgnoreCase.DeepClone()).IsTrue();
        await Assert.That(StringComparer.InvariantCulture == StringComparer.InvariantCulture.DeepClone()).IsTrue();
        await Assert.That(StringComparer.InvariantCultureIgnoreCase == StringComparer.InvariantCultureIgnoreCase.DeepClone()).IsTrue();
    }

    private async Task Check_Comparer_does_not_Clone_Internal<T>()
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        EqualityComparer<T> cloned = comparer.DeepClone();

        // checking by reference
        await Assert.That(comparer == cloned).IsTrue();
    }
}