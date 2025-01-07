using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Tracing;
using System.Drawing;
using System.Dynamic;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace FastCloner.Tests;

[TestFixture]
public class SpecificScenariosTest
{
    [Test]
    public void Test_ExpressionTree_OrderBy1()
    {
        IOrderedQueryable<int> q = Enumerable.Range(1, 5).Reverse().AsQueryable().OrderBy(x => x);
        IOrderedQueryable<int> q2 = q.DeepClone();
        Assert.That(q2.ToArray()[0], Is.EqualTo(1));
        Assert.That(q.ToArray().Length, Is.EqualTo(5));
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
}