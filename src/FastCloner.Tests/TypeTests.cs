using System.IO;
using System.Collections;
using Microsoft.Win32.SafeHandles;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace FastCloner.Tests;
public class TypeTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    [Test]
    public async Task StandardTypes_Should_Be_Cloned()
    {
        StringBuilder b = new StringBuilder();
        b.Append("test1");
        StringBuilder cloned = b.DeepClone();
        await Assert.That(cloned.ToString()).IsEqualTo("test1");
        int[] arr = [1, 2, 3];
        IEnumerator enumerator = arr.GetEnumerator();
        enumerator.MoveNext();
        IEnumerator enumCloned = enumerator.DeepClone();
        enumerator.MoveNext();
        await Assert.That(enumCloned.Current).IsEqualTo(1);
    }

    [Test]
    [Property("Description", "Just for fun, do not clone such object in real situation")]
    public async Task Type_With_Native_Resource_Should_Be_Cloned()
    {
        string fileName = Path.GetTempFileName();
        try
        {
            StreamWriter writer = File.CreateText(fileName);
            writer.AutoFlush = true;
            writer.Write("1");
            StreamWriter cloned = writer.DeepClone();
            writer.Write("2");
            cloned.Write("3");

            writer.Dispose();
            // cloned.Close();
            // not a bug anymore, this is feature: we don't duplicate handles, because it can cause unpredictable results
            // ~~this was a bug, we should not throw there~~

            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                cloned.Flush();
                await Task.CompletedTask;
            });
            string res = File.ReadAllText(fileName);

            // it uses RandomAccess.WriteAtOffset(this._fileHandle, buffer, this._filePosition); - and offset of cloned file
            // is preserved, so, 2 will disappear
            await Assert.That(res).IsEqualTo("13");
        }
        finally
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
        }
    }

    [Test]
    public async Task Funcs_Should_Be_Cloned()
    {
        string[] closure = ["123"];
        Func<int, string> f = x => closure[0] + x.ToString(CultureInfo.InvariantCulture);
        Func<int, string> df = f.DeepClone();
        Func<int, string> cf = f.ShallowClone();
        closure[0] = "xxx";
        
        using (Assert.Multiple())
        {
            await Assert.That(f(3)).IsEqualTo("xxx3");
            // delegates are shallow-copied by default, sharing the same closure
            await Assert.That(df(3)).IsEqualTo("xxx3");
            await Assert.That(cf(3)).IsEqualTo("xxx3");

        }
    }

    public class EventHandlerTest1
    {
        public event Action<int>? Event;

        public int Call(int x)
        {
            if (Event != null)
            {
                Event(x);
                return Event.GetInvocationList().Length;
            }

            return 0;
        }
    }

    [Test]
    [Property("Description", "Some libraries notifies about problems with this types")]
    public async Task Events_Should_Be_Cloned()
    {
        EventHandlerTest1 eht = new EventHandlerTest1();
        int[] summ = new int[1];
        Action<int> a1 = x => summ[0] += x;
        Action<int> a2 = x => summ[0] += x;
        eht.Event += a1;
        eht.Event += a2;
        eht.Call(1);
        await Assert.That(summ[0]).IsEqualTo(2);
        EventHandlerTest1 clone = eht.DeepClone();
        clone.Call(1);
        // delegates are shallow-copied, so the clone shares the same handlers
        await Assert.That(summ[0]).IsEqualTo(4);
        eht.Event -= a1;
        eht.Event -= a2;
        
        using (Assert.Multiple())
        {
            await Assert.That(eht.Call(1)).IsEqualTo(0);
            await Assert.That(summ[0]).IsEqualTo(4);
            await Assert.That(clone.Call(1)).IsEqualTo(2);
            await Assert.That(summ[0]).IsEqualTo(6);

        }
    }

    private const string CertData =
        @"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";

    [Test]
    [Property("Description", "Without special handling it causes exception on destruction due native resources usage")]
    public void Certificate_Should_Be_Cloned()
    {
        X509Certificate2 cert = new X509Certificate2(Convert.FromBase64String(CertData), "1");
        cert.DeepClone();
        cert.DeepClone();
        GC.Collect();
    }

    [Test]
    [Property("Description", "Without special handling it causes exception on destruction due native resources usage")]
    public void Certificate_Should_Be_Shallow_Cloned()
    {
        X509Certificate2 cert = new X509Certificate2(Convert.FromBase64String(CertData), "1");
        cert.ShallowClone();
        cert.ShallowClone();
        GC.Collect();
    }
}