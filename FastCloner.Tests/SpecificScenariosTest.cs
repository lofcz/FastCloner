using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Tracing;
using System.Drawing;
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