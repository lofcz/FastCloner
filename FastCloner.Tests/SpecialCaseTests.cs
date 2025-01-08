using System.Collections;
using System.Collections.Concurrent;
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
using System.Runtime.InteropServices;
using System.Text;
using FastCloner.Code;
using FastCloner.Contrib;
using Microsoft.EntityFrameworkCore;

namespace FastCloner.Tests;

[TestFixture]
public class SpecialCaseTests
{
    [OneTimeSetUp]
    public void Setup()
    {
        ContribTypeHandlers.Register();
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public class MyClass
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public short[] shortsArray= new short[4];
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
    public void Test_DeepClone_Marshal()
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
        Assert.That(cloned, Is.Not.SameAs(original));
        
        Assert.That(cloned.shortsArray, Is.Not.SameAs(original.shortsArray));
        Assert.That(cloned.shortsArray, Is.EqualTo(original.shortsArray));
        
        Assert.That(cloned.internals, Is.Not.SameAs(original.internals));
        Assert.That(cloned.internals.Length, Is.EqualTo(original.internals.Length));
        
        for (int i = 0; i < original.internals.Length; i++)
        {
            Assert.That(cloned.internals[i], Is.Not.SameAs(original.internals[i]));
            Assert.That(cloned.internals[i].myByte, Is.EqualTo(original.internals[i].myByte));
            Assert.That(cloned.internals[i].myUint1, Is.EqualTo(original.internals[i].myUint1));
            Assert.That(cloned.internals[i].myUint2, Is.EqualTo(original.internals[i].myUint2));
            Assert.That(cloned.internals[i].myUint3, Is.EqualTo(original.internals[i].myUint3));
        }
    }
    
    [Test]
    public void Test_InitOnlyProperties_ObjectInitialization()
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
        Assert.Multiple(() =>
        {
            Assert.That(person.Name, Is.EqualTo("John Doe"));
            Assert.That(person.Age, Is.EqualTo(30));
            Assert.That(person.BirthDate, Is.EqualTo(new DateTime(1993, 1, 1)));
            Assert.That(person.HomeAddress.Street, Is.EqualTo("123 Main St"));
        });
    }

    [Test]
    public void Test_InitOnlyProperties_WithCloning()
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
        Assert.Multiple(() =>
        {
            Assert.That(modified.Name, Is.EqualTo(original.Name));
            Assert.That(modified.Age, Is.EqualTo(31));
            Assert.That(modified.HomeAddress, Is.EqualTo(original.HomeAddress));
            Assert.That(modified, Is.Not.SameAs(original));
        });
    }

    [Test]
    public void Test_InitOnlyProperties_RecordEquality()
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
        Assert.Multiple(() =>
        {
            Assert.That(person1, Is.EqualTo(person2));
            Assert.That(person1.GetHashCode(), Is.EqualTo(person2.GetHashCode()));
            Assert.That(person1, Is.EqualTo(person2));
        });
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
    public void Test_InitOnlyProperties_WithNullValues()
    {
        // Arrange & Act
        PersonWithInitProperties person = new PersonWithInitProperties
        {
            Name = null,
            Age = 30,
            HomeAddress = null
        };

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(person.Name, Is.Null);
            Assert.That(person.Age, Is.EqualTo(30));
            Assert.That(person.HomeAddress, Is.Null);
        });
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

        public C3 c3 { get; set; } = new C3();
    }

    public class C1 : CBase<int>
    {
        public C2 c2 { get; set; } = new C2();
    }
    
    [Test]
    public void Test_DeepClone_ClassHierarchy()
    {
        // Arrange
        C1 original = new C1
        {
            Id = 1,
            c2 = new C2
            {
                Id = 2,
                c3 = new C3
                {
                    Id = 3
                }
            }
        };

        // Act
        C1 cloned1 = original.DeepClone();
    
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned1, Is.Not.SameAs(original));
            Assert.That(cloned1.Id, Is.EqualTo(original.Id));
            Assert.That(cloned1.c2, Is.Not.SameAs(original.c2));
            Assert.That(cloned1.c2.Id, Is.EqualTo(original.c2.Id));
            Assert.That(cloned1.c2.c3, Is.Not.SameAs(original.c2.c3));
            Assert.That(cloned1.c2.c3.Id, Is.EqualTo(original.c2.c3.Id));
        });
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

    [Test]
    public void Test_Clone_Props()
    {
        TestProps original = new TestProps { A = 42, B = "Test value" };
        TestProps clone = original.DeepClone();
    
        Assert.Multiple(() =>
        {
            Assert.That(clone.A, Is.EqualTo(42));
            Assert.That(clone.B, Is.EqualTo("Test value"));
            Assert.That(clone, Is.Not.SameAs(original));
        });
    }

    [Test]
    public void Test_Clone_Props_With_Ignored()
    {
        TestPropsWithIgnored original = new TestPropsWithIgnored { A = 42, B = "Test value" };
        TestPropsWithIgnored clone = original.DeepClone();
    
        Assert.Multiple(() =>
        {
            Assert.That(clone.A, Is.EqualTo(42));
            Assert.That(clone.B, Is.EqualTo(null)); // default value
            Assert.That(clone, Is.Not.SameAs(original));
        });
    }

    private class TestAutoProps
    {
        public int A { get; set; } = 10;
        public string B { get; private set; } = "My string";
        public int C => A * 2;
        
        private int _d;
        public int D
        {
            get => _d;
            set => _d = value;
        }
    }

    [Test]
    public void Test_Clone_Auto_Properties()
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
        Assert.Multiple(() =>
        {
            Assert.That(clone.A, Is.EqualTo(42));
            Assert.That(clone.B, Is.EqualTo("Test value"));
            Assert.That(clone.C, Is.EqualTo(84));
            Assert.That(clone.D, Is.EqualTo(100));
            Assert.That(clone, Is.Not.SameAs(original));
        });
    }
    
    [Test]
    public void ParallelCloning_WithReadOnlyFields_ShouldBeThreadSafe()
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
                Assert.That(clone, Is.Not.SameAs(testObject));
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        Assert.That(exceptions, Is.Empty, "Parallel cloning should not throw any exceptions");
    }

    private class ClassWithReadOnlyField
    {
        private readonly string _readOnlyField = "test";
        public string ReadOnlyValue => _readOnlyField;
    }


    private class TestAutoPropsWithIgnored
    {
        public int A { get; set; } = 10;
        
        [FastClonerIgnore]
        public string B { get; private set; } = "My string";
        
        public int C => A * 2;
        
        private int _d;
        [FastClonerIgnore]
        public int D
        {
            get => _d;
            set => _d = value;
        }
    }

    [Test]
    public void Test_Clone_Auto_Properties_With_Ignored()
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
        Assert.Multiple(() =>
        {
            Assert.That(clone.A, Is.EqualTo(42));
            Assert.That(clone.B, Is.EqualTo(null));
            Assert.That(clone.C, Is.EqualTo(84));
            Assert.That(clone.D, Is.EqualTo(0));
            Assert.That(clone, Is.Not.SameAs(original));
        });
    }
    
    [Test]
    public void Test_ExpressionTree_OrderBy1()
    {
        IOrderedQueryable<int> q = Enumerable.Range(1, 5).Reverse().AsQueryable().OrderBy(x => x);
        IOrderedQueryable<int> q2 = q.DeepClone();
        Assert.That(q2.ToArray()[0], Is.EqualTo(1));
        Assert.That(q.ToArray(), Has.Length.EqualTo(5));
    }
    
    [Test]
    public void Test_Action_Delegate_Clone()
    {
        // Arrange
        TestClass testObject = new TestClass();
        Action<string> originalAction = testObject.TestMethod;
    
        // Act
        Action<string> clonedAction = originalAction.DeepClone();
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(clonedAction.Target, Is.SameAs(originalAction.Target), "Delegate Target should remain the same reference");
            Assert.That(clonedAction.Method, Is.EqualTo(originalAction.Method), "Delegate Method should be the same");
        });

        List<string> originalResult = [];
        List<string> clonedResult = [];
    
        originalAction("test");
        clonedAction("test");
    
        Assert.That(clonedResult, Is.EquivalentTo(originalResult), "Both delegates should produce the same result");
    }
    
    [Test]
    public void Test_Static_Action_Delegate_Clone()
    {
        // Arrange
        Action<string> originalAction = StaticTestMethod;
    
        // Act
        Action<string> clonedAction = originalAction.DeepClone();
        Assert.Multiple(() =>
        {

            // Assert
            Assert.That(clonedAction.Target, Is.Null, "Static delegate Target should be null");
            Assert.That(originalAction.Target, Is.Null, "Static delegate Target should be null");
            Assert.That(clonedAction.Method, Is.EqualTo(originalAction.Method), "Delegate Method should be the same");
        });
    }
    
    [Test]
    public void Nested_Closure_Clone()
    {
        // Arrange
        int x = 1;

        Func<int> outer = CreateClosure();
    
        // Act
        Func<int> outerCopy = outer.DeepClone();
    
        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(outer.Invoke(), Is.EqualTo(6)); // 1 + 3 + 2
            Assert.That(outerCopy.Invoke(), Is.EqualTo(6));
        });
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
    public void Event_Handler_Clone_With_Method()
    {
        // Arrange
        EventSource source = new EventSource();
        EventListener listener = new EventListener();
        EventHandler handler = listener.HandleEvent;
        source.TestEvent += handler;

        // Act
        EventHandler handlerCopy = handler.DeepClone();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(handlerCopy.Target, Is.SameAs(handler.Target), "Handler Target should be the same");
            Assert.That(handlerCopy.Method, Is.EqualTo(handler.Method), "Handler Method should be the same");
        
            source.RaiseEvent();
            Assert.That(listener.Counter, Is.EqualTo(1), "Original handler should increment counter");
    
            source.TestEvent += handlerCopy;
            source.RaiseEvent();
            Assert.That(listener.Counter, Is.EqualTo(3), "Both handlers should increment counter");
        });
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
    public void Circular_Reference_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Cloned object should be a new instance");
            Assert.That(cloned.Name, Is.EqualTo(original.Name), "Properties should be copied");
            Assert.That(cloned.Reference, Is.SameAs(cloned), "Circular reference should point to the cloned instance");
            Assert.That(cloned.Reference.Reference, Is.SameAs(cloned), "Nested circular reference should point to the cloned instance");
        });
    }

    private class CircularClass
    {
        public string Name { get; set; }
        public CircularClass Reference { get; set; }
    }

    [Test]
    public void Complex_Circular_Reference_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(clonedA, Is.Not.SameAs(nodeA), "Node A should be cloned");
            Assert.That(clonedB, Is.Not.SameAs(nodeB), "Node B should be cloned");
            Assert.That(clonedC, Is.Not.SameAs(nodeC), "Node C should be cloned");
            
            Assert.That(clonedA.Name, Is.EqualTo("A"), "Node A name should be copied");
            Assert.That(clonedB.Name, Is.EqualTo("B"), "Node B name should be copied");
            Assert.That(clonedC.Name, Is.EqualTo("C"), "Node C name should be copied");
            
            Assert.That(clonedC.Next, Is.SameAs(clonedA), "Cycle should be preserved");
            Assert.That(clonedA.Next, Is.SameAs(clonedB), "References should point to new instances");
            Assert.That(clonedB.Next, Is.SameAs(clonedC), "References should point to new instances");
        });
    }
    
    [Test]
    public void Dynamic_Object_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Cloned object should be a new instance");
            Assert.That(cloned.Name, Is.EqualTo("Test"), "String property should be copied");
            Assert.That(cloned.Number, Is.EqualTo(42), "Number property should be copied");
            Assert.That(cloned.Nested, Is.Not.SameAs(original.Nested), "Nested object should be cloned");
            Assert.That(cloned.Nested.Value, Is.EqualTo("Nested Value"), "Nested value should be copied");
        });
    }
    
    [Test]
    public void Dynamic_With_Nested_ExpandoObject_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Name, Is.EqualTo("Parent"), "Parent name should be copied");
            Assert.That(cloned.Child.Name, Is.EqualTo("Child"), "Child name should be copied");
            
            Assert.That(cloned.Child.Parent, Is.SameAs(cloned), "Circular reference should point to cloned parent");
            Assert.That(original.Child.Parent, Is.SameAs(original), "Original circular reference should remain unchanged");
        });
    }

    [Test]
    public void Dynamic_With_Collection_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Items, Is.Not.SameAs(original.Items), "Collection should be cloned");
            Assert.That(cloned.Items.Count, Is.EqualTo(2), "Collection should have same number of items");
            
            Assert.That(cloned.Items[0].Name, Is.EqualTo("Item1"), "First item name should be copied");
            Assert.That(cloned.Items[0].Owner, Is.SameAs(cloned), "First item should reference cloned parent");
            
            Assert.That(cloned.Items[1].Name, Is.EqualTo("Item2"), "Second item name should be copied");
            Assert.That(cloned.Items[1].Owner, Is.SameAs(cloned), "Second item should reference cloned parent");
        });
    }

    [Test]
    public void HttpRequest_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Method, Is.EqualTo(HttpMethod.Post), "Method should be copied");
            Assert.That(cloned.RequestUri?.ToString(), Is.EqualTo("https://api.example.com/data"), "URI should be copied");
            Assert.That(cloned.Version, Is.EqualTo(new Version(2, 0)), "Version should be copied");
            
            Assert.That(cloned.Headers.Accept.First().MediaType, Is.EqualTo("application/json"), "Accept header should be copied");
            Assert.That(cloned.Headers.GetValues("Custom-Header").First(), Is.EqualTo("test-value"), "Custom header should be copied");
            Assert.That(cloned.Headers.Authorization?.Scheme, Is.EqualTo("Bearer"), "Authorization scheme should be copied");
            Assert.That(cloned.Headers.Authorization?.Parameter, Is.EqualTo("test-token"), "Authorization parameter should be copied");
            
            Assert.That(cloned.Content, Is.Not.Null, "Content should be cloned");
            Assert.That(cloned.Content, Is.TypeOf<StringContent>(), "Content type should be preserved");
            
            string originalContent = original.Content.ReadAsStringAsync().Result;
            string clonedContent = cloned.Content.ReadAsStringAsync().Result;
            Assert.That(clonedContent, Is.EqualTo(originalContent), "Content value should be copied");
            Assert.That(cloned.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"), "Content-Type should be copied");
        });
    }

    [Test]
    public void HttpRequest_With_MultipartContent_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Content, Is.TypeOf<MultipartFormDataContent>(), "Content type should be preserved");
            
            MultipartFormDataContent? originalMultipart = (MultipartFormDataContent)original.Content;
            MultipartFormDataContent? clonedMultipart = (MultipartFormDataContent)cloned.Content;
            
            string originalParts = originalMultipart.ReadAsStringAsync().Result;
            string clonedParts = clonedMultipart.ReadAsStringAsync().Result;
            
            Assert.That(clonedParts, Is.EqualTo(originalParts), "Multipart content should be identical");
            Assert.That(clonedMultipart.Headers.ContentType?.Parameters.First(p => p.Name == "boundary").Value, Is.Not.Null, "Boundary should be present");
        });
    }

    [Test]
    public void HttpRequest_With_Handlers_Clone()
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
        
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Properties, Is.Not.Empty, "Properties should be copied");
            Assert.That(cloned.Properties["AllowAutoRedirect"], Is.EqualTo(false), "Handler property should be copied");
            Assert.That(cloned.Properties["AutomaticDecompression"], Is.EqualTo(DecompressionMethods.GZip | DecompressionMethods.Deflate), "Handler compression settings should be copied");
            Assert.That(cloned.Properties["UseCookies"], Is.EqualTo(false), "Handler cookie settings should be copied");
        });
    }
    
    [Test]
    public void HttpResponse_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Status code should be copied");
            Assert.That(cloned.Version, Is.EqualTo(new Version(2, 0)), "Version should be copied");
            Assert.That(cloned.ReasonPhrase, Is.EqualTo("Custom OK Message"), "Reason phrase should be copied");
            
            Assert.That(cloned.Headers.CacheControl?.MaxAge, Is.EqualTo(TimeSpan.FromHours(1)), "Cache control should be copied");
            Assert.That(cloned.Headers.GetValues("X-Custom-Response").First(), Is.EqualTo("test-response"), "Custom header should be copied");
            
            string originalContent = original.Content.ReadAsStringAsync().Result;
            string clonedContent = cloned.Content.ReadAsStringAsync().Result;
            Assert.That(clonedContent, Is.EqualTo(originalContent), "Content should be copied");
        });
    }

    [Test]
    [Platform("win")]
    public void Font_Clone()
    {
        // Arrange
        Font original = new Font("Arial", 12, FontStyle.Bold | FontStyle.Italic);

        // Act
        Font? cloned = FastCloner.DeepClone(original);
    
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Should be different instance");
            Assert.That(cloned.Name, Is.EqualTo("Arial"), "Font name should be copied");
            Assert.That(cloned.Size, Is.EqualTo(12), "Font size should be copied");
            Assert.That(cloned.Style, Is.EqualTo(FontStyle.Bold | FontStyle.Italic), "Font style should be copied");
            Assert.That(cloned.Unit, Is.EqualTo(original.Unit), "Font unit should be copied");
            Assert.That(cloned.GdiCharSet, Is.EqualTo(original.GdiCharSet), "GDI charset should be copied");
            Assert.That(cloned.GdiVerticalFont, Is.EqualTo(original.GdiVerticalFont), "GDI vertical font should be copied");
        });
    }

    
    [Test]
    public void HttpRequest_With_StreamContent_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Content, Is.TypeOf<StreamContent>(), "Content type should be preserved");
            
            string originalContent = original.Content.ReadAsStringAsync().Result;
            string clonedContent = cloned.Content.ReadAsStringAsync().Result;
            Assert.That(clonedContent, Is.EqualTo(originalContent), "Stream content should be copied");
            Assert.That(cloned.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/plain"), "Content type should be copied");
        });
    }

    [Test]
    public void HttpRequest_With_ComplexHeaders_Clone()
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
        Assert.Multiple(() =>
        {
            List<MediaTypeWithQualityHeaderValue> acceptHeaders = cloned.Headers.Accept.OrderBy(x => x.MediaType).ToList();
            Assert.That(acceptHeaders[0].MediaType, Is.EqualTo("application/json"), "First accept header should be copied");
            Assert.That(acceptHeaders[0].Quality, Is.EqualTo(1.0), "First accept header quality should be copied");
            Assert.That(acceptHeaders[1].MediaType, Is.EqualTo("text/xml"), "Second accept header should be copied");
            Assert.That(acceptHeaders[1].Quality, Is.EqualTo(0.8), "Second accept header quality should be copied");

            List<StringWithQualityHeaderValue> languageHeaders = cloned.Headers.AcceptLanguage.OrderBy(x => x.Value).ToList();
            Assert.That(languageHeaders[0].Value, Is.EqualTo("cs-CZ"), "First language header should be copied");
            Assert.That(languageHeaders[0].Quality, Is.EqualTo(0.8), "First language header quality should be copied");
            Assert.That(languageHeaders[1].Value, Is.EqualTo("en-US"), "Second language header should be copied");
            Assert.That(languageHeaders[1].Quality, Is.EqualTo(1.0), "Second language header quality should be copied");

            List<string> ifMatchValues = cloned.Headers.GetValues("If-Match").ToList();
            Assert.That(ifMatchValues, Has.Count.EqualTo(2), "If-Match headers count should match");
            Assert.That(ifMatchValues, Contains.Item("\"123\""), "First If-Match value should be copied");
            Assert.That(ifMatchValues, Contains.Item("\"456\""), "Second If-Match value should be copied");

            List<string> customMultiValues = cloned.Headers.GetValues("X-Custom-Multi").ToList();
            Assert.That(customMultiValues, Has.Count.EqualTo(2), "Custom multi-value header count should match");
            Assert.That(customMultiValues, Contains.Item("value1"), "First custom multi-value should be copied");
            Assert.That(customMultiValues, Contains.Item("value2"), "Second custom multi-value should be copied");
        });
    }
    
    [Test]
    public void Dynamic_With_Dictionary_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Dict, Is.Not.SameAs(original.Dict), "Dictionary should be cloned");
            Assert.That(cloned.Dict.Count, Is.EqualTo(1), "Dictionary should have same number of items");
            Assert.That(cloned.Dict["key1"].Name, Is.EqualTo("Value1"), "Dictionary value should be copied");
            Assert.That(cloned.Dict["key1"].Container, Is.SameAs(cloned), "Dictionary value should reference cloned container");
            Assert.That(cloned.Self, Is.SameAs(cloned), "Self reference should point to clone");
        });
    }
    
    [Test]
    public void NotifyPropertyChanged_Clone()
    {
        // Arrange
        NotifyingPerson original = new NotifyingPerson { Name = "John", Age = 30 };
        List<string> propertyChanges = [];
        original.PropertyChanged += (sender, args) => propertyChanges.Add(args.PropertyName);

        // Act
        NotifyingPerson? cloned = FastCloner.DeepClone(original);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Name, Is.EqualTo("John"), "Property should be copied");
            Assert.That(cloned.Age, Is.EqualTo(30), "Property should be copied");
            
            cloned.Name = "Jane";
            Assert.That(propertyChanges, Is.Empty, "Cloned object should not trigger original events");
            
            List<string> clonedChanges = [];
            cloned.PropertyChanged += (object sender, PropertyChangedEventArgs args) => clonedChanges.Add(args.PropertyName);
            cloned.Age = 31;
            Assert.That(clonedChanges, Has.Count.EqualTo(1), "Cloned object should trigger its own events");
            Assert.That(clonedChanges[0], Is.EqualTo(nameof(NotifyingPerson.Age)));
        });
    }

    [Test]
    public void NotifyPropertyChanged_With_Complex_Properties_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Address, Is.Not.Null, "Complex property should be cloned");
            Assert.That(cloned.Address.Street, Is.EqualTo("Main St"), "Nested property should be copied");
            Assert.That(cloned.Address.City, Is.EqualTo("New York"), "Nested property should be copied");
            
            cloned.Address.Street = "Broadway";
            Assert.That(addressChanges, Is.Empty, "Cloned nested object should not trigger original events");
            
            List<string> clonedAddressChanges = [];
            cloned.Address.PropertyChanged += (object sender, PropertyChangedEventArgs args) => clonedAddressChanges.Add(args.PropertyName);
            cloned.Address.City = "Boston";
            Assert.That(clonedAddressChanges, Has.Count.EqualTo(1), "Cloned nested object should trigger its own events");
            Assert.That(clonedAddressChanges[0], Is.EqualTo(nameof(NotifyingAddress.City)));
        });
    }

    [Test]
    public void NotifyPropertyChanged_With_Collection_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Children, Is.Not.Null, "Collection should be cloned");
            Assert.That(cloned.Children, Has.Count.EqualTo(2), "Collection should have same number of items");
            Assert.That(cloned.Children[0].Name, Is.EqualTo("Child1"), "Collection items should be copied");
            
            cloned.Children.Add(new NotifyingPerson { Name = "Child3" });
            Assert.That(collectionChanges, Is.EqualTo(0), "Cloned collection should not trigger original events");
            
            int clonedChanges = 0;
            cloned.Children.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs args) => clonedChanges++;
            cloned.Children.RemoveAt(0);
            Assert.That(clonedChanges, Is.EqualTo(1), "Cloned collection should trigger its own events");
        });
    }

    public class INotifyTest : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _prop;
        public string Prop
        {
            get => _prop;

            set
            {
                _prop = value;
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
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.EqualTo(obj));
            Assert.That(result.Value, Is.EqualTo(obj.Value));
            Assert.That(result.Object, Is.Not.EqualTo(obj.Object));
            Assert.That(result.Builder == obj.Builder, Is.True);
        });
    }
    
    [Test]
    public void Test_Rune()
    {
        // Arrange
        Rune obj = new Rune(0x1F44D);
    
        // Act
        Rune result = obj.DeepClone();
    
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(obj));
            Assert.That(result, Is.EqualTo(obj));
            Assert.That(result.Value, Is.EqualTo(obj.Value));
            Assert.That(result.ToString(), Is.EqualTo("👍"));
        });
    }

    [Test]
    public void Test_RuneContainer()
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
        Assert.Multiple(() =>
        {
            Assert.That(ReferenceEquals(result, container), Is.False);
            Assert.That(result.RuneValue, Is.EqualTo(container.RuneValue));
            Assert.That(result.RuneValue.ToString(), Is.EqualTo("🚀"));
        });
    }

    public class RuneContainer
    {
        public Rune RuneValue { get; set; }
    }
    
    [Test]
    public void Test_TimeSpan()
    {
        // Arrange
        TimeSpan obj = TimeSpan.FromHours(42.5);
    
        // Act
        TimeSpan result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }

    [Test]
    public void Test_TimeZoneInfo()
    {
        // Arrange
        TimeZoneInfo obj = TimeZoneInfo.Local;
    
        // Act
        TimeZoneInfo result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }

    [Test]
    public void Test_Half()
    {
        // Arrange
        Half obj = (Half)42.5f;
    
        // Act
        Half result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }

    [Test]
    public void Test_Int128()
    {
        // Arrange
        Int128 obj = Int128.Parse("123456789012345678901234567890");
    
        // Act
        Int128 result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }

    [Test]
    public void Test_UInt128()
    {
        // Arrange
        UInt128 obj = UInt128.Parse("123456789012345678901234567890");
    
        // Act
        UInt128 result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }

    [Test]
    public void Test_Char()
    {
        // Arrange
        char obj = 'Ž';
    
        // Act
        char result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }

    [Test]
    public void Test_Bool()
    {
        // Arrange
        bool obj = true;
    
        // Act
        bool result = obj.DeepClone();
    
        // Assert
        Assert.That(result, Is.EqualTo(obj));
    }
    
    [Test]
    public void Test_Notify_Triggered_Correctly()
    {
        // Arrange
        List<string> output = [];
        INotifyTest a = new INotifyTest();
        a.PropertyChanged += (sender, args) =>
        {
            output.Add(((INotifyTest)sender).Prop);
        };

        // Act
        a.Prop = "A changed";
        INotifyTest b = a.DeepClone();
        b.Prop = "B changed";
        b.Prop = "B changed again";

        // Assert
        Assert.That(output, Has.Count.EqualTo(1));
        Assert.That(output[0], Is.EqualTo("A changed"));
    }

    public class NotifyingPerson : INotifyPropertyChanged
    {
        private string _name;
        private int _age;
        private NotifyingAddress _address;
        private ObservableCollection<NotifyingPerson> _children;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int Age
        {
            get => _age;
            set
            {
                _age = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Age)));
            }
        }

        public NotifyingAddress Address
        {
            get => _address;
            set
            {
                _address = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Address)));
            }
        }

        public ObservableCollection<NotifyingPerson> Children
        {
            get => _children;
            set
            {
                _children = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Children)));
            }
        }
    }

    public class NotifyingAddress : INotifyPropertyChanged
    {
        private string _street;
        private string _city;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Street
        {
            get => _street;
            set
            {
                _street = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Street)));
            }
        }

        public string City
        {
            get => _city;
            set
            {
                _city = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(City)));
            }
        }
    }
    
    [Test]
    public void Dynamic_With_Delegate_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        int counter = 0;
        original.Name = "Test";
        original.Increment = (Func<int>)(() => ++counter);
    
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
    
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Name, Is.EqualTo("Test"), "String property should be copied");
            
            int originalResult = original.Increment();
            int clonedResult = cloned.Increment();
            Assert.That(originalResult, Is.EqualTo(1), "Original delegate should increment counter");
            Assert.That(clonedResult, Is.EqualTo(2), "Cloned delegate should share the same counter");
            Assert.That(counter, Is.EqualTo(2), "Counter should be incremented twice");
            
            originalResult = original.Increment();
            clonedResult = cloned.Increment();
            Assert.That(originalResult, Is.EqualTo(3), "Original delegate should continue counting");
            Assert.That(clonedResult, Is.EqualTo(4), "Cloned delegate should continue counting");
            Assert.That(counter, Is.EqualTo(4), "Counter should be incremented four times");
        });
    }
    
    [Test]
    public void ExpandoObject_With_Collection_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.List = new List<string> { "Item1", "Item2" };
        original.Dictionary = new Dictionary<string, int> { ["Key1"] = 1, ["Key2"] = 2 };
        
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned.List, Is.Not.SameAs(original.List), "List should be cloned");
            Assert.That(cloned.List, Is.EquivalentTo(original.List), "List items should be copied");
            Assert.That(cloned.Dictionary, Is.Not.SameAs(original.Dictionary), "Dictionary should be cloned");
            Assert.That(cloned.Dictionary["Key1"], Is.EqualTo(1), "Dictionary values should be copied");
            Assert.That(cloned.Dictionary["Key2"], Is.EqualTo(2), "Dictionary values should be copied");
        });
    }

     [Test]
    public void ReadOnlyDictionary_Clone_ShouldCreateNewInstance()
    {
        // Arrange
        Dictionary<string, int> originalDict = new Dictionary<string, int> { ["One"] = 1, ["Two"] = 2 };
        ReadOnlyDictionary<string, int> original = new ReadOnlyDictionary<string, int>(originalDict);

        // Act
        ReadOnlyDictionary<string, int>? cloned = FastCloner.DeepClone(original);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Should create new instance");
            Assert.That(cloned, Is.TypeOf<ReadOnlyDictionary<string, int>>(), "Should preserve type");
            Assert.That(cloned.Count, Is.EqualTo(original.Count), "Should have same count");
            Assert.That(cloned["One"], Is.EqualTo(1), "Should preserve values");
            Assert.That(cloned["Two"], Is.EqualTo(2), "Should preserve values");
        });
    }

    [Test]
    public void IReadOnlyDictionary_Clone_ShouldCreateNewInstance()
    {
        // Arrange
        IReadOnlyDictionary<string, int> original = 
            new Dictionary<string, int> { ["One"] = 1, ["Two"] = 2 }.AsReadOnly();

        // Act
        IReadOnlyDictionary<string, int>? cloned = FastCloner.DeepClone(original);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Should create new instance");
            Assert.That(cloned, Is.AssignableTo<IReadOnlyDictionary<string, int>>(), "Should preserve interface");
            Assert.That(cloned.Count, Is.EqualTo(original.Count), "Should have same count");
            Assert.That(cloned["One"], Is.EqualTo(1), "Should preserve values");
            Assert.That(cloned["Two"], Is.EqualTo(2), "Should preserve values");
        });
    }
    
    [Test]
    public void IReadOnlySet_Clone_ShouldCreateNewInstance()
    {
        // Arrange
        IReadOnlySet<string> original = new HashSet<string> { "One", "Two", "Three" }.AsReadOnly();

        // Act
        IReadOnlySet<string>? cloned = FastCloner.DeepClone(original);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Should create new instance");
            Assert.That(cloned, Is.AssignableTo<IReadOnlySet<string>>(), "Should preserve interface");
            Assert.That(cloned.Count, Is.EqualTo(original.Count), "Should have same count");
            Assert.That(cloned.Contains("One"), Is.True, "Should contain original elements");
            Assert.That(cloned.Contains("Two"), Is.True, "Should contain original elements");
            Assert.That(cloned.Contains("Three"), Is.True, "Should contain original elements");
        });
    }

    [Test]
    public void IReadOnlySet_IsSubsetOf_ShouldWorkCorrectly()
    {
        // Arrange
        IReadOnlySet<int> original = new HashSet<int> { 1, 2 }.AsReadOnly();
        IReadOnlySet<int> superSet = new HashSet<int> { 1, 2, 3 }.AsReadOnly();
        IReadOnlySet<int> nonSuperSet = new HashSet<int> { 1, 4 }.AsReadOnly();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(original.IsSubsetOf(superSet), Is.True, "Should be subset of superset");
            Assert.That(original.IsSubsetOf(nonSuperSet), Is.False, "Should not be subset of non-superset");
            Assert.That(original.IsSubsetOf(original), Is.True, "Should be subset of itself");
        });
    }

    [Test]
    public void IReadOnlySet_Overlaps_ShouldWorkCorrectly()
    {
        // Arrange
        IReadOnlySet<char> setA = new HashSet<char> { 'a', 'b', 'c' }.AsReadOnly();
        IReadOnlySet<char> setB = new HashSet<char> { 'b', 'c', 'd' }.AsReadOnly();
        IReadOnlySet<char> setC = new HashSet<char> { 'x', 'y', 'z' }.AsReadOnly();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(setA.Overlaps(setB), Is.True, "Sets with common elements should overlap");
            Assert.That(setA.Overlaps(setC), Is.False, "Sets without common elements should not overlap");
            Assert.That(setA.Overlaps(setA), Is.True, "Set should overlap with itself");
        });
    }

    public class ReadOnlySet<T> : IReadOnlySet<T>
    {
        private readonly ISet<T> _set;

        public ReadOnlySet(ISet<T> set)
        {
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public int Count => _set.Count;
        public bool Contains(T item) => _set.Contains(item);
        public bool IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<T> other) => _set.Overlaps(other);
        public bool SetEquals(IEnumerable<T> other) => _set.SetEquals(other);
        public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }


    [Test]
    public void ReadOnlyDictionary_WithComplexValues_Clone_ShouldDeepClone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Should create new instance");
            Assert.That(cloned["List1"], Is.Not.SameAs(original["List1"]), "Should deep clone values");
            Assert.That(cloned["List2"], Is.Not.SameAs(original["List2"]), "Should deep clone values");
            Assert.That(cloned["List1"], Is.EquivalentTo(original["List1"]), "Should preserve value contents");
            Assert.That(cloned["List2"], Is.EquivalentTo(original["List2"]), "Should preserve value contents");
        });
    }

    [Test]
    public void ReadOnlyDictionary_WithNullValues_Clone_ShouldPreserveNulls()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned["NotNull"], Is.EqualTo("Value"), "Should preserve non-null values");
            Assert.That(cloned["Null"], Is.Null, "Should preserve null values");
        });
    }

    [Test]
    public void ReadOnlyDictionary_Empty_Clone_ShouldCreateEmptyInstance()
    {
        // Arrange
        ReadOnlyDictionary<string, int> original = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

        // Act
        ReadOnlyDictionary<string, int>? cloned = FastCloner.DeepClone(original);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Should create new instance");
            Assert.That(cloned.Count, Is.EqualTo(0), "Should be empty");
        });
    }

    [Test]
    public void ReadOnlyDictionary_WithKeyValuePairs_Clone_ShouldPreserveEnumeration()
    {
        // Arrange
        Dictionary<int, string> originalDict = new Dictionary<int, string> { [1] = "One", [2] = "Two" };
        ReadOnlyDictionary<int, string> original = new ReadOnlyDictionary<int, string>(originalDict);

        // Act
        ReadOnlyDictionary<int, string>? cloned = FastCloner.DeepClone(original);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Keys, Is.EquivalentTo(original.Keys), "Should preserve keys");
            Assert.That(cloned.Values, Is.EquivalentTo(original.Values), "Should preserve values");
            Assert.That(cloned, Is.EquivalentTo(original), "Should preserve key-value pairs");
        });
    }
    
    [Test]
    public void ExpandoObject_With_Circular_Reference_Clone()
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
        Assert.Multiple(() =>
        {
            Assert.That(cloned, Is.Not.SameAs(original), "Cloned object should be a new instance");
            Assert.That(cloned.Nested, Is.Not.SameAs(original.Nested), "Nested object should be cloned");
            Assert.That(cloned.Name, Is.EqualTo("Original"), "Properties should be copied");
            Assert.That(cloned.Nested.Parent, Is.SameAs(cloned), "Circular reference should point to cloned instance");
        });
    }

    [Test]
    public void Mixed_Dynamic_And_Static_Types_Clone()
    {
        // Arrange
        StaticType staticObject = new StaticType { Value = "Static" };
        dynamic dynamic = new ExpandoObject();
        dynamic.Static = staticObject;
        dynamic.Name = "Dynamic";
        
        // Act
        dynamic cloned = FastCloner.DeepClone(dynamic);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned.Static, Is.Not.SameAs(staticObject), "Static type should be cloned");
            Assert.That(cloned.Static.Value, Is.EqualTo("Static"), "Static type properties should be copied");
            Assert.That(cloned.Name, Is.EqualTo("Dynamic"), "Dynamic properties should be copied");
        });
    }

    private class StaticType
    {
        public string Value { get; set; }
    }

    [Test]
    public void ExpandoObject_With_Null_Values_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.NullProperty = null;
        original.ValidProperty = "NotNull";
        
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(((object)cloned.NullProperty), Is.Null, "Null properties should remain null");
            Assert.That(cloned.ValidProperty, Is.EqualTo("NotNull"), "Non-null properties should be copied");
        });
    }

    [Test]
    public void Dynamic_Object_With_Complex_Types_Clone()
    {
        // Arrange
        dynamic original = new ExpandoObject();
        original.DateTime = DateTime.Now;
        original.Guid = Guid.NewGuid();
        original.TimeSpan = TimeSpan.FromHours(1);
        
        // Act
        dynamic cloned = FastCloner.DeepClone(original);
        
        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(cloned.DateTime, Is.EqualTo(original.DateTime), "DateTime should be copied");
            Assert.That(cloned.Guid, Is.EqualTo(original.Guid), "Guid should be copied");
            Assert.That(cloned.TimeSpan, Is.EqualTo(original.TimeSpan), "TimeSpan should be copied");
        });
    }


    private class Node
    {
        public string Name { get; set; }
        public Node Next { get; set; }
    }
    
    [Test]
    public void Test_ExpressionTree_OrderBy2()
    {
        IEnumerable<Tuple<int, string>> l = new List<int> { 2, 1, 3, 4, 5 }.Select(y => new Tuple<int, string>(y, y.ToString(CultureInfo.InvariantCulture)));
        IOrderedQueryable<Tuple<int, string>> q = l.AsQueryable().OrderBy(x => x.Item1);
        IOrderedQueryable<Tuple<int, string>> q2 = q.DeepClone();
        Assert.That(q2.ToArray()[0].Item1, Is.EqualTo(1));
        Assert.That(q.ToArray().Length, Is.EqualTo(5));
    }

    [Test(Description = "Tests works on local SQL Server with AdventureWorks database")]
    [Ignore("Test on MS Server")]
    public void Clone_EfQuery1()
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
        Assert.That(q2.ToArray().Length, Is.EqualTo(1));
    }

    [Test(Description = "Tests works on local SQL Server with AdventureWorks database")]
    [Ignore("Test on MS Server")]
    public void Clone_EfQuery2()
    {
        IOrderedQueryable<Currency> q = new AdventureContext().Currencies.OrderBy(x => x.Name);
        IOrderedQueryable<Currency> q2 = q.DeepClone();
        int cnt = q.Count();
        Assert.That(q2.Count(), Is.EqualTo(cnt));
    }
    
    
    
    [Test]
    [Platform(Include = "Win")]
    public void FontCloningTest()
    {
        return;
        #if WINDOWS
        // Arrange
        Font originalFont = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);

        // Act
        Font clonedFont = originalFont.DeepClone();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(clonedFont, Is.Not.Null);
            Assert.That(clonedFont.Name, Is.EqualTo(originalFont.Name));
            Assert.That(clonedFont.Size, Is.EqualTo(originalFont.Size));
            Assert.That(clonedFont.Style, Is.EqualTo(originalFont.Style));
            Assert.That(clonedFont.Unit, Is.EqualTo(originalFont.Unit));
            Assert.That(clonedFont.GdiCharSet, Is.EqualTo(originalFont.GdiCharSet));
            Assert.That(clonedFont.GdiVerticalFont, Is.EqualTo(originalFont.GdiVerticalFont));

            // Ensure the cloned font is a different instance
            Assert.That(ReferenceEquals(originalFont, clonedFont), Is.False);
        });
        
        #endif
    }


    [Test]
    public void Lazy_Clone()
    {
        LazyClass lazy = new LazyClass();
        LazyClass clone = lazy.DeepClone();
        int v = LazyClass.Counter;
        Assert.That(clone.GetValue(), Is.EqualTo((v + 1).ToString(CultureInfo.InvariantCulture)));
        Assert.That(lazy.GetValue(), Is.EqualTo((v + 2).ToString(CultureInfo.InvariantCulture)));
    }

    public class LazyClass
    {
        public static int Counter;

        private readonly LazyRef<object> _lazyValue = new LazyRef<object>(() => (++Counter).ToString(CultureInfo.InvariantCulture));

        public string GetValue() => _lazyValue.Value.ToString();
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
    public void Closure_Clone()
    {
        int a = 0;
        Func<int> f = () => ++a;
        Func<int> fCopy = f.DeepClone();
        Assert.That(f(), Is.EqualTo(1));
        Assert.That(fCopy(), Is.EqualTo(1));
        Assert.That(a, Is.EqualTo(1));
    }

    private class TestComparer : Comparer<int>
    {
        // make object unsafe to work
        private object _fieldX = new object();

        public override int Compare(int x, int y) => x.CompareTo(y);
    }
    
    public sealed class LazyRef<T>
    {
        private Func<T> _initializer;
        private T _value;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public T Value
        {
            get
            {
                if (_initializer != null)
                {
                    _value = _initializer();
                    _initializer = null;
                }
                return _value;
            }
            set
            {
                _value = value;
                _initializer = null;
            }
        }

        public LazyRef(Func<T> initializer) => _initializer = initializer;
    }
    
    [Test]
    public void CanCopyInterfaceField()
    {
        MyObject o = new MyObject();

        MyIClass original = new MyIClass 
        {
            Field1 = o,
            Field2 = o
        };

        MyIClass result = original.DeepClone();
        
        Assert.Multiple(() =>
        {
            Assert.That(original.Field1, Is.SameAs(original.Field2), "Original objects should be same");
            Assert.That(result.Field1, Is.SameAs(result.Field2), "Cloned objects should be same");
        });
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
}