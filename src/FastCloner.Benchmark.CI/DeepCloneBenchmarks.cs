using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using Cloner = global::FastCloner.FastCloner;
using DeepClonerExt = Force.DeepCloner.DeepClonerExtensions;

namespace FastCloner.Benchmark.CI;

[MemoryDiagnoser]
[SimpleJob]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[CategoriesColumn]
public class DeepCloneBenchmarks
{
    private const int Seed = 42;
    private const int TenMegabytes = 10 * 1024 * 1024;
    private const int StringArrayLength = 1000;
    private const int ObjectListCount = 100;
    private const int ObjectDictionaryCount = 50;

    private SmallObject _smallObject = null!;
    private SmallObjectWithCollections _smallObjectWithCollections = null!;
    private MediumNestedObject _mediumNestedObject = null!;
    private FileSpec _fileSpec = null!;
    private LargeEventDocument _largeEventDocument = null!;
    private LargeLogBatch _largeLogBatch = null!;
    private ObjectWithDynamicProperties _dynamicWithDictionary = null!;
    private ObjectWithDynamicProperties _dynamicWithNestedObject = null!;
    private ObjectWithDynamicProperties _dynamicWithArray = null!;
    private string[] _stringArray = null!;
    private List<MediumNestedObject> _objectList = null!;
    private Dictionary<string, LargeEventDocument> _objectDictionary = null!;

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(Seed);

        _smallObject = CreateSmallObject(random);
        _smallObjectWithCollections = CreateSmallObjectWithCollections(random);
        _mediumNestedObject = CreateMediumNestedObject(random);
        _fileSpec = CreateFileSpec(random);
        _largeEventDocument = CreateLargeEventDocument(random, TenMegabytes);
        _largeLogBatch = CreateLargeLogBatch(random, TenMegabytes);
        _dynamicWithDictionary = CreateDynamicWithDictionary(random);
        _dynamicWithNestedObject = CreateDynamicWithNestedObject(random);
        _dynamicWithArray = CreateDynamicWithArray(random);
        _stringArray = CreateStringArray(random, StringArrayLength);
        _objectList = CreateObjectList(random, ObjectListCount);
        _objectDictionary = CreateObjectDictionary(random, ObjectDictionaryCount);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SmallObject")]
    public SmallObject DeepCloner_SmallObject() => DeepClonerExt.DeepClone(_smallObject)!;

    [Benchmark]
    [BenchmarkCategory("SmallObject")]
    public SmallObject FastCloner_SmallObject() => Cloner.DeepClone(_smallObject)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SmallObjectWithCollections")]
    public SmallObjectWithCollections DeepCloner_SmallObjectWithCollections() => DeepClonerExt.DeepClone(_smallObjectWithCollections)!;

    [Benchmark]
    [BenchmarkCategory("SmallObjectWithCollections")]
    public SmallObjectWithCollections FastCloner_SmallObjectWithCollections() => Cloner.DeepClone(_smallObjectWithCollections)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MediumNestedObject")]
    public MediumNestedObject DeepCloner_MediumNestedObject() => DeepClonerExt.DeepClone(_mediumNestedObject)!;

    [Benchmark]
    [BenchmarkCategory("MediumNestedObject")]
    public MediumNestedObject FastCloner_MediumNestedObject() => Cloner.DeepClone(_mediumNestedObject)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FileSpec")]
    public FileSpec DeepCloner_FileSpec() => DeepClonerExt.DeepClone(_fileSpec)!;

    [Benchmark]
    [BenchmarkCategory("FileSpec")]
    public FileSpec FastCloner_FileSpec() => Cloner.DeepClone(_fileSpec)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LargeEventDocument_10MB")]
    public LargeEventDocument DeepCloner_LargeEventDocument_10MB() => DeepClonerExt.DeepClone(_largeEventDocument)!;

    [Benchmark]
    [BenchmarkCategory("LargeEventDocument_10MB")]
    public LargeEventDocument FastCloner_LargeEventDocument_10MB() => Cloner.DeepClone(_largeEventDocument)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LargeLogBatch_10MB")]
    public LargeLogBatch DeepCloner_LargeLogBatch_10MB() => DeepClonerExt.DeepClone(_largeLogBatch)!;

    [Benchmark]
    [BenchmarkCategory("LargeLogBatch_10MB")]
    public LargeLogBatch FastCloner_LargeLogBatch_10MB() => Cloner.DeepClone(_largeLogBatch)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DynamicWithDictionary")]
    public ObjectWithDynamicProperties DeepCloner_DynamicWithDictionary() => DeepClonerExt.DeepClone(_dynamicWithDictionary)!;

    [Benchmark]
    [BenchmarkCategory("DynamicWithDictionary")]
    public ObjectWithDynamicProperties FastCloner_DynamicWithDictionary() => Cloner.DeepClone(_dynamicWithDictionary)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DynamicWithNestedObject")]
    public ObjectWithDynamicProperties DeepCloner_DynamicWithNestedObject() => DeepClonerExt.DeepClone(_dynamicWithNestedObject)!;

    [Benchmark]
    [BenchmarkCategory("DynamicWithNestedObject")]
    public ObjectWithDynamicProperties FastCloner_DynamicWithNestedObject() => Cloner.DeepClone(_dynamicWithNestedObject)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DynamicWithArray")]
    public ObjectWithDynamicProperties DeepCloner_DynamicWithArray() => DeepClonerExt.DeepClone(_dynamicWithArray)!;

    [Benchmark]
    [BenchmarkCategory("DynamicWithArray")]
    public ObjectWithDynamicProperties FastCloner_DynamicWithArray() => Cloner.DeepClone(_dynamicWithArray)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("StringArray_1000")]
    public string[] DeepCloner_StringArray_1000() => DeepClonerExt.DeepClone(_stringArray)!;

    [Benchmark]
    [BenchmarkCategory("StringArray_1000")]
    public string[] FastCloner_StringArray_1000() => Cloner.DeepClone(_stringArray)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ObjectList_100")]
    public List<MediumNestedObject> DeepCloner_ObjectList_100() => DeepClonerExt.DeepClone(_objectList)!;

    [Benchmark]
    [BenchmarkCategory("ObjectList_100")]
    public List<MediumNestedObject> FastCloner_ObjectList_100() => Cloner.DeepClone(_objectList)!;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ObjectDictionary_50")]
    public Dictionary<string, LargeEventDocument> DeepCloner_ObjectDictionary_50() => DeepClonerExt.DeepClone(_objectDictionary)!;

    [Benchmark]
    [BenchmarkCategory("ObjectDictionary_50")]
    public Dictionary<string, LargeEventDocument> FastCloner_ObjectDictionary_50() => Cloner.DeepClone(_objectDictionary)!;

    private static SmallObject CreateSmallObject(Random random)
    {
        return new SmallObject
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365)),
            IsActive = random.Next(2) == 1,
            Score = random.NextDouble() * 100
        };
    }

    private static SmallObjectWithCollections CreateSmallObjectWithCollections(Random random)
    {
        return new SmallObjectWithCollections
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            Tags = GenerateStringList(random, 10, 20),
            Metadata = GenerateStringDictionary(random, 5, 30)
        };
    }

    private static MediumNestedObject CreateMediumNestedObject(Random random)
    {
        return new MediumNestedObject
        {
            Id = Guid.NewGuid(),
            Type = GenerateString(random, 30),
            Source = GenerateString(random, 100),
            Message = GenerateString(random, 500),
            Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(10000)),
            Level = random.Next(1, 6),
            Tags = GenerateStringList(random, 5, 15),
            Properties = GenerateStringDictionary(random, 10, 50),
            User = new UserInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = GenerateString(random, 30),
                Email = $"{GenerateString(random, 10)}@example.com",
                Roles = GenerateStringList(random, 3, 20)
            },
            Request = new RequestInfo
            {
                Method = random.Next(4) switch { 0 => "GET", 1 => "POST", 2 => "PUT", _ => "DELETE" },
                Path = $"/api/{GenerateString(random, 20)}/{random.Next(1000)}",
                QueryString = GenerateStringDictionary(random, 3, 20),
                Headers = GenerateStringDictionary(random, 8, 100),
                ClientIp = $"{random.Next(256)}.{random.Next(256)}.{random.Next(256)}.{random.Next(256)}",
                UserAgent = GenerateString(random, 150)
            }
        };
    }

    private static FileSpec CreateFileSpec(Random random)
    {
        return new FileSpec
        {
            Path = $"/storage/{GenerateString(random, 20)}/{GenerateString(random, 30)}.dat",
            Created = DateTime.UtcNow.AddDays(-random.Next(365)),
            Modified = DateTime.UtcNow.AddHours(-random.Next(24)),
            Size = random.Next(1000, 10000000),
            Data = GenerateStringDictionary(random, 5, 50)
        };
    }

    private static LargeEventDocument CreateLargeEventDocument(Random random, int targetSizeBytes)
    {
        const int stackFrameCount = 100;
        const int extendedDataCount = 200;
        int extendedDataStringLength = Math.Max(100, targetSizeBytes / extendedDataCount / 2);

        List<StackFrameInfo> stackFrames = new(stackFrameCount);
        for (int i = 0; i < stackFrameCount; i++)
        {
            stackFrames.Add(new StackFrameInfo
            {
                FileName = $"/src/{GenerateString(random, 30)}/{GenerateString(random, 40)}.cs",
                LineNumber = random.Next(1, 5000),
                ColumnNumber = random.Next(1, 200),
                MethodName = GenerateString(random, 50),
                TypeName = $"{GenerateString(random, 30)}.{GenerateString(random, 40)}",
                Namespace = $"Company.{GenerateString(random, 20)}.{GenerateString(random, 20)}",
                Parameters = GenerateStringList(random, 5, 30),
                LocalVariables = GenerateStringDictionary(random, 3, 50)
            });
        }

        Dictionary<string, object> extendedData = new(extendedDataCount);
        for (int i = 0; i < extendedDataCount; i++)
            extendedData[$"data_{i}"] = GenerateString(random, extendedDataStringLength);

        return new LargeEventDocument
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            ProjectId = Guid.NewGuid().ToString(),
            StackId = Guid.NewGuid().ToString(),
            Type = "error",
            Source = GenerateString(random, 200),
            Message = GenerateString(random, 2000),
            Date = DateTime.UtcNow.AddMinutes(-random.Next(10000)),
            Count = random.Next(1, 1000),
            IsFirstOccurrence = random.Next(2) == 1,
            IsFixed = random.Next(2) == 1,
            IsHidden = random.Next(2) == 1,
            Tags = GenerateStringList(random, 20, 30),
            Geo = $"{random.NextDouble() * 180 - 90},{random.NextDouble() * 360 - 180}",
            Value = random.NextDouble() * 10000,
            StackTrace = stackFrames,
            ExtendedData = extendedData,
            ReferenceIds = GenerateStringList(random, 10, 36),
            User = new UserInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = GenerateString(random, 50),
                Email = $"{GenerateString(random, 15)}@example.com",
                Roles = GenerateStringList(random, 5, 20)
            },
            Request = new RequestInfo
            {
                Method = "POST",
                Path = $"/api/{GenerateString(random, 30)}/{random.Next(10000)}",
                QueryString = GenerateStringDictionary(random, 10, 50),
                Headers = GenerateStringDictionary(random, 20, 200),
                ClientIp = $"{random.Next(256)}.{random.Next(256)}.{random.Next(256)}.{random.Next(256)}",
                UserAgent = GenerateString(random, 300)
            },
            Environment = new EnvironmentInfo
            {
                MachineName = GenerateString(random, 30),
                ProcessorCount = random.Next(1, 128),
                TotalPhysicalMemory = random.NextInt64(1024L * 1024 * 1024, 256L * 1024 * 1024 * 1024),
                AvailablePhysicalMemory = random.NextInt64(1024L * 1024 * 1024, 64L * 1024 * 1024 * 1024),
                OsName = "Windows 11",
                OsVersion = "10.0.22631",
                Architecture = "x64",
                RuntimeVersion = ".NET 10.0.0",
                ProcessName = GenerateString(random, 30),
                ProcessId = random.Next(1, 65535),
                CommandLine = GenerateString(random, 500),
                EnvironmentVariables = GenerateStringDictionary(random, 30, 100)
            }
        };
    }

    private static LargeLogBatch CreateLargeLogBatch(Random random, int targetSizeBytes)
    {
        int entryCount = targetSizeBytes / 3000;

        List<LogEntry> entries = new(entryCount);
        for (int i = 0; i < entryCount; i++)
        {
            entries.Add(new LogEntry
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMilliseconds(-random.Next(1000000)),
                Level = random.Next(6) switch
                {
                    0 => "Trace",
                    1 => "Debug",
                    2 => "Info",
                    3 => "Warn",
                    4 => "Error",
                    _ => "Fatal"
                },
                Category = $"{GenerateString(random, 20)}.{GenerateString(random, 30)}",
                Message = GenerateString(random, 500),
                Exception = random.Next(10) == 0 ? GenerateString(random, 2000) : null,
                Properties = GenerateStringDictionary(random, 5, 100),
                Scopes = GenerateStringList(random, 3, 50),
                TraceId = Guid.NewGuid().ToString("N"),
                SpanId = random.NextInt64().ToString("x16"),
                ParentSpanId = random.Next(2) == 1 ? random.NextInt64().ToString("x16") : null
            });
        }

        return new LargeLogBatch
        {
            BatchId = Guid.NewGuid(),
            Source = GenerateString(random, 100),
            CreatedAt = DateTime.UtcNow,
            Entries = entries,
            Metadata = GenerateStringDictionary(random, 10, 50)
        };
    }

    private static ObjectWithDynamicProperties CreateDynamicWithDictionary(Random random)
    {
        return new ObjectWithDynamicProperties
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            DynamicData = GenerateStringDictionary(random, 20, 100),
            NestedDynamic = new Dictionary<string, object>
            {
                ["nested1"] = GenerateStringDictionary(random, 5, 50),
                ["nested2"] = GenerateStringList(random, 10, 30),
                ["nested3"] = random.NextDouble()
            }
        };
    }

    private static ObjectWithDynamicProperties CreateDynamicWithNestedObject(Random random)
    {
        return new ObjectWithDynamicProperties
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            DynamicData = CreateSmallObject(random),
            NestedDynamic = CreateMediumNestedObject(random)
        };
    }

    private static ObjectWithDynamicProperties CreateDynamicWithArray(Random random)
    {
        object[] array = new object[100];
        for (int i = 0; i < array.Length; i++)
        {
            int mod = i % 3;
            array[i] = mod switch
            {
                0 => GenerateString(random, 100),
                1 => random.NextDouble(),
                _ => CreateSmallObject(random)
            };
        }

        return new ObjectWithDynamicProperties
        {
            Id = random.Next(),
            Name = GenerateString(random, 50),
            DynamicData = array,
            NestedDynamic = new object[]
            {
                CreateSmallObject(random),
                GenerateStringList(random, 5, 20),
                random.Next()
            }
        };
    }

    private static string[] CreateStringArray(Random random, int count)
    {
        string[] array = new string[count];
        for (int i = 0; i < count; i++)
            array[i] = GenerateString(random, 50 + random.Next(100));
        return array;
    }

    private static List<MediumNestedObject> CreateObjectList(Random random, int count)
    {
        List<MediumNestedObject> list = new(count);
        for (int i = 0; i < count; i++)
            list.Add(CreateMediumNestedObject(random));
        return list;
    }

    private static Dictionary<string, LargeEventDocument> CreateObjectDictionary(Random random, int count)
    {
        Dictionary<string, LargeEventDocument> dict = new(count);
        for (int i = 0; i < count; i++)
            dict[$"event_{i}"] = CreateMediumEventDocument(random);
        return dict;
    }

    private static LargeEventDocument CreateMediumEventDocument(Random random)
    {
        List<StackFrameInfo> stackFrames = new(5);
        for (int i = 0; i < 5; i++)
        {
            stackFrames.Add(new StackFrameInfo
            {
                FileName = $"/src/{GenerateString(random, 20)}/{GenerateString(random, 20)}.cs",
                LineNumber = random.Next(1, 1000),
                ColumnNumber = random.Next(1, 100),
                MethodName = GenerateString(random, 30),
                TypeName = GenerateString(random, 40),
                Namespace = GenerateString(random, 30),
                Parameters = GenerateStringList(random, 3, 20),
                LocalVariables = GenerateStringDictionary(random, 2, 30)
            });
        }

        Dictionary<string, object> extendedData = new(10);
        for (int i = 0; i < 10; i++)
            extendedData[$"data_{i}"] = GenerateString(random, 200);

        return new LargeEventDocument
        {
            Id = Guid.NewGuid().ToString(),
            OrganizationId = Guid.NewGuid().ToString(),
            ProjectId = Guid.NewGuid().ToString(),
            StackId = Guid.NewGuid().ToString(),
            Type = "error",
            Source = GenerateString(random, 50),
            Message = GenerateString(random, 200),
            Date = DateTime.UtcNow.AddMinutes(-random.Next(10000)),
            Count = random.Next(1, 100),
            IsFirstOccurrence = random.Next(2) == 1,
            IsFixed = random.Next(2) == 1,
            IsHidden = random.Next(2) == 1,
            Tags = GenerateStringList(random, 5, 20),
            Geo = $"{random.NextDouble() * 180 - 90},{random.NextDouble() * 360 - 180}",
            Value = random.NextDouble() * 1000,
            StackTrace = stackFrames,
            ExtendedData = extendedData,
            ReferenceIds = GenerateStringList(random, 3, 36),
            User = new UserInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = GenerateString(random, 30),
                Email = $"{GenerateString(random, 10)}@example.com",
                Roles = GenerateStringList(random, 2, 15)
            },
            Request = new RequestInfo
            {
                Method = "POST",
                Path = $"/api/{GenerateString(random, 20)}",
                QueryString = GenerateStringDictionary(random, 3, 30),
                Headers = GenerateStringDictionary(random, 5, 50),
                ClientIp = $"{random.Next(256)}.{random.Next(256)}.{random.Next(256)}.{random.Next(256)}",
                UserAgent = GenerateString(random, 100)
            },
            Environment = new EnvironmentInfo
            {
                MachineName = GenerateString(random, 20),
                ProcessorCount = random.Next(1, 32),
                TotalPhysicalMemory = random.NextInt64(1024L * 1024 * 1024, 64L * 1024 * 1024 * 1024),
                AvailablePhysicalMemory = random.NextInt64(1024L * 1024 * 1024, 32L * 1024 * 1024 * 1024),
                OsName = "Windows 11",
                OsVersion = "10.0.22631",
                Architecture = "x64",
                RuntimeVersion = ".NET 10.0.0",
                ProcessName = GenerateString(random, 20),
                ProcessId = random.Next(1, 65535),
                CommandLine = GenerateString(random, 100),
                EnvironmentVariables = GenerateStringDictionary(random, 10, 50)
            }
        };
    }

    private static string GenerateString(Random random, int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 _-";
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[random.Next(chars.Length)];
        return new string(result);
    }

    private static List<string> GenerateStringList(Random random, int count, int stringLength)
    {
        List<string> list = new(count);
        for (int i = 0; i < count; i++)
            list.Add(GenerateString(random, stringLength));
        return list;
    }

    private static Dictionary<string, string> GenerateStringDictionary(Random random, int count, int valueLength)
    {
        Dictionary<string, string> dict = new(count);
        for (int i = 0; i < count; i++)
            dict[$"key_{i}"] = GenerateString(random, valueLength);
        return dict;
    }
}

public class SmallObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public double Score { get; set; }
}

public class SmallObjectWithCollections
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class MediumNestedObject
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int Level { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new();
    public UserInfo User { get; set; } = new();
    public RequestInfo Request { get; set; } = new();
}

public class FileSpec
{
    public string Path { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public long Size { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class RequestInfo
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, string> QueryString { get; set; } = new();
    public Dictionary<string, string> Headers { get; set; } = new();
    public string ClientIp { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
}

public class StackFrameInfo
{
    public string FileName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int ColumnNumber { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
    public Dictionary<string, string> LocalVariables { get; set; } = new();
}

public class EnvironmentInfo
{
    public string MachineName { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long TotalPhysicalMemory { get; set; }
    public long AvailablePhysicalMemory { get; set; }
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string CommandLine { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

public class LargeEventDocument
{
    public string Id { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string StackId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int Count { get; set; }
    public bool IsFirstOccurrence { get; set; }
    public bool IsFixed { get; set; }
    public bool IsHidden { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Geo { get; set; } = string.Empty;
    public double Value { get; set; }
    public List<StackFrameInfo> StackTrace { get; set; } = new();
    public Dictionary<string, object> ExtendedData { get; set; } = new();
    public List<string> ReferenceIds { get; set; } = new();
    public UserInfo User { get; set; } = new();
    public RequestInfo Request { get; set; } = new();
    public EnvironmentInfo Environment { get; set; } = new();
}

public class LogEntry
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
    public List<string> Scopes { get; set; } = new();
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string? ParentSpanId { get; set; }
}

public class LargeLogBatch
{
    public Guid BatchId { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<LogEntry> Entries { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ObjectWithDynamicProperties
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public object DynamicData { get; set; } = null!;
    public object NestedDynamic { get; set; } = null!;
}
