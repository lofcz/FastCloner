using AutoMapper;
using BenchmarkDotNet.Attributes;
using FastCloner.SourceGenerator.Shared;
using Force.DeepCloner;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NClone;
using Newtonsoft.Json;
using ProtoBuf;
using Riok.Mapperly.Abstractions;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;

namespace FastCloner.Benchmark;

[RankColumn]
[Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public partial class BenchMinimal
{
    private TestObject testData;
    private IMapper mapper;
    private TestObjectMapper mapperlyMapper;
    
    [GlobalSetup]
    public void Setup()
    {
        testData = new TestObject
        {
            Id = 1,
            Name = "Test",
            NestedObject = new NestedObject
            {
                Value = 42,
                Description = "Nested test"
            }
        };

        var config = new MapperConfiguration((IMapperConfigurationExpression cfg) => {
            cfg.CreateMap<TestObject, TestObject>();
            cfg.CreateMap<NestedObject, NestedObject>();
        }, new NullLoggerFactory());
        mapper = config.CreateMapper();
        
        mapperlyMapper = new TestObjectMapper();
    }
    
    [Benchmark(Baseline = true)]
    public object? FastCloner()
    {
        return testData.FastDeepClone();
    }

    [Benchmark]
    public object? DeepCopier()
    {
        return global::DeepCopier.Copier.Copy(testData);
    }

    [Benchmark]
    public object? DeepCopy()
    {
        return global::DeepCopy.DeepCopier.Copy(testData);
    }

    [Benchmark]
    public object DeepCopyExpression()
    {
        return global::DeepCopy.ObjectCloner.Clone(testData);
    }

    [Benchmark]
    public object? FastDeepCloner()
    {
        return global::FastDeepCloner.DeepCloner.Clone(testData);
    }
    
    [Benchmark]
    public object? DeepCloner()
    {
        return testData.DeepClone();
    }
    
    [Benchmark]
    public object? ObjectCloner()
    {
        return global::ObjectCloner.ObjectCloner.DeepClone(testData);
    }

    [Benchmark]
    public object? NewtonsoftJson()
    {
        var json = JsonConvert.SerializeObject(testData);
        return JsonConvert.DeserializeObject<TestObject>(json);
    }

    [Benchmark]
    public object? SystemTextJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(testData);
        return System.Text.Json.JsonSerializer.Deserialize<TestObject>(json);
    }

    // crashes
    /*[Benchmark]
    public object? BinaryFormatter()
    {
#pragma warning disable SYSLIB0011
        using var stream = new MemoryStream();
        var formatter = new BinaryFormatter();
        formatter.Serialize(stream, testData);
        stream.Seek(0, SeekOrigin.Begin);
        return formatter.Deserialize(stream);
#pragma warning restore SYSLIB0011
    }*/

    [Benchmark]
    public object? MessagePack()
    {
        var bytes = MessagePackSerializer.Serialize(testData);
        return MessagePackSerializer.Deserialize<TestObject>(bytes);
    }

    [Benchmark]
    public object? ProtobufNet()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, testData);
        stream.Seek(0, SeekOrigin.Begin);
        return Serializer.Deserialize<TestObject>(stream);
    }

    [Benchmark]
    public object? AutoMapper()
    {
        return mapper.Map<TestObject>(testData);
    }

    [Benchmark]
    public object? AnyClone()
    {
        return global::AnyClone.CloneExtensions.Clone(testData);
    }

    [Benchmark]
    public object? NClone()
    {
        return Clone.ObjectGraph(testData);
    }

    [Benchmark]
    public object? Mapperly()
    {
        return mapperlyMapper.TestObjectToTestObject(testData);
    }


    [Serializable]
    [FastClonerClonable]
    [FastClonerTrustNullability]
    [MessagePackObject]
    [ProtoContract]
    public class TestObject
    {
        [Key(0)]
        [ProtoMember(1)]
        public int Id { get; set; }
        [Key(1)]
        [ProtoMember(2)]
        public string Name { get; set; }
        [Key(2)]
        [ProtoMember(3)]
        public NestedObject NestedObject { get; set; }
    }

    [Serializable]
    [MessagePackObject]
    [ProtoContract]
    public class NestedObject
    {
        [Key(0)]
        [ProtoMember(1)]
        public int Value { get; set; }
        [Key(1)]
        [ProtoMember(2)]
        public string Description { get; set; }
    }

    [Mapper(UseDeepCloning = true)]
    public partial class TestObjectMapper
    {
        public partial TestObject TestObjectToTestObject(TestObject testObject);
    }
}
