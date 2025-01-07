using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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