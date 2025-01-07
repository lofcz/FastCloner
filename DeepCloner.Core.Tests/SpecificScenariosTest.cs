using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
#if OLDFRAMEWORK
using System.Data.Entity;
#else
using Microsoft.EntityFrameworkCore;
#endif

namespace DeepCloner.Core.Tests;

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
#if !OLDFRAMEWORK
            // Console.WriteLine(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            //	.GetRequiredService<Microsoft.EntityFrameworkCore.ChangeTracking.Internal.IChangeTrackerFactory>(
            //		((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>) at).Instance));
            /* Console.WriteLine(((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>) at).Instance);
            var serviceProvider = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>) at.DeepClone()).Instance;
            Console.WriteLine(serviceProvider);
            Console.WriteLine(serviceProvider.GetService(typeof(Microsoft.EntityFrameworkCore.ChangeTracking.Internal.IChangeTrackerFactory)));
            Console.WriteLine(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.EntityFrameworkCore.ChangeTracking.Internal.IChangeTrackerFactory>(
                    serviceProvider));*/
#endif
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
    public void Clone_ComObject1()
    {
#if OLDFRAMEWORK
// ReSharper disable SuspiciousTypeConversion.Global
        var manager = (KnownFolders.IKnownFolderManager)new KnownFolders.KnownFolderManager();
// ReSharper restore SuspiciousTypeConversion.Global
        Guid knownFolderId1;
        Guid knownFolderId2;
        manager.FolderIdFromCsidl(0, out knownFolderId1);
        manager.DeepClone().FolderIdFromCsidl(0, out knownFolderId2);
        Assert.That(knownFolderId1, Is.EqualTo(knownFolderId2));
#endif
    }

    [Test]
    public void Clone_ComObject2()
    {
#if OLDFRAMEWORK
        Type t = Type.GetTypeFromProgID("SAPI.SpVoice");
        var obj = Activator.CreateInstance(t);
        obj.DeepClone();
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
#if OLDFRAMEWORK
            : base("Server=.;Integrated Security=SSPI;Database=AdventureWorks")
#endif
        {
        }

        public DbSet<Currency> Currencies { get; set; }

#if !OLDFRAMEWORK
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer(@"Server=.;Database=AdventureWorks;Trusted_Connection=true;MultipleActiveResultSets=true;Encrypt=False");
#endif
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

#if OLDFRAMEWORK
    [Test]
    public void Windows_Forms_Clone()
    {
        var form = new System.Windows.Forms.Form();
        form.Controls.Add(new System.Windows.Forms.ComboBox());
        form.Controls.Add(new System.Windows.Forms.Button());
        form.Controls.Add(new System.Windows.Forms.CheckBox());
        for (var i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            form.DeepClone();
            Console.WriteLine(sw.ElapsedMilliseconds);
        }
    }
#endif

    private class TestComparer : Comparer<int>
    {
        // make object unsafe to work
        private object _fieldX = new object();

        public override int Compare(int x, int y) => x.CompareTo(y);
    }

#if OLDFRAMEWORK
    public class KnownFolders
    {
        [Guid("8BE2D872-86AA-4d47-B776-32CCA40C7018"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IKnownFolderManager
        {
            void FolderIdFromCsidl(int csidl, [Out] out Guid knownFolderID);

            void FolderIdToCsidl([In] [MarshalAs(UnmanagedType.LPStruct)] Guid id, [Out] out int csidl);

            void GetFolderIds();
        }

        [ComImport, Guid("4df0c730-df9d-4ae3-9153-aa6b82e9795a")]
        internal class KnownFolderManager
        {
            // make object unsafe to work
            #pragma warning disable 169
            private object _fieldX;
            #pragma warning restore 169
        }
    }
#endif
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