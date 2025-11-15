using BenchmarkDotNet.Attributes;
using Dolly;
using FastCloner.SourceGenerator.Shared;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class BenchDolly
{
    private readonly ComplexClass _complexClass;

    public BenchDolly()
    {
        _complexClass = new ComplexClass
        {
            SimpleClass2 = new SimpleClass3()
            {
                Int = 10,
                UInt = 1231,
                Long = 1231234561L,
                ULong = 1516524352UL,
                Double = 1235.1235762,
                Float = 1.333F,
                String = "Lorem ipsum ...",
            },
            Array = [
                new SimpleClass3()
                {
                    Int = 10,
                    UInt = 1231,
                    Long = 1231234561L,
                    ULong = 1516524352UL,
                    Double = 1235.1235762,
                    Float = 1.333F,
                    String = "Lorem ipsum ...",
                },
                new SimpleClass3()
                {
                    Int = 10,
                    UInt = 1231,
                    Long = 1231234561L,
                    ULong = 1516524352UL,
                    Double = 1235.1235762,
                    Float = 1.333F,
                    String = "Lorem ipsum ...",
                },
            ],
            List = [
                new SimpleClass3()
                {
                    Int = 10,
                    UInt = 1231,
                    Long = 1231234561L,
                    ULong = 1516524352UL,
                    Double = 1235.1235762,
                    Float = 1.333F,
                    String = "Lorem ipsum ...",
                },
                new SimpleClass3()
                {
                    Int = 10,
                    UInt = 1231,
                    Long = 1231234561L,
                    ULong = 1516524352UL,
                    Double = 1235.1235762,
                    Float = 1.333F,
                    String = "Lorem ipsum ...",
                },
            ]
        };
    }

    [Benchmark(Baseline = true, Description = "Dolly")]
    public void TestDolly()
    {
        var clone = _complexClass.DeepClone();
    }

    [Benchmark(Description = "FastCloner")]
    public void TestFastCloner()
    {
        var clone = _complexClass.FastDeepClone();
    }
}

[Clonable]
[FastClonerClonable]
public partial class SimpleClass3
{
    public int Int { get; set; }
    public uint UInt { get; set; }
    public long Long { get; set; }
    public ulong ULong { get; set; }
    public double Double { get; set; }
    public float Float { get; set; }
    public string String { get; set; }
}

[Clonable]
[FastClonerClonable]
public partial class ComplexClass
{
    public SimpleClass3 SimpleClass2 { get; set; }
    public SimpleClass3[] Array { get; set; }
    public List<SimpleClass3> List { get; set; }
}

