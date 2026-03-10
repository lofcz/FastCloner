using System.IO;
using System.Data.Common;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using FluentNHibernate.Mapping;
using NHibernate.Cfg;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Collection;
using NHibernate.Mapping.ByCode;
using NHibernate.Tool.hbm2ddl;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace FastCloner.Tests;

using NHibernate;
using NHibernate.Proxy;
[NotInParallel("FastClonerGlobalState")]
public class DbTests(int maxRecursionDepth) : BaseTestFixture(maxRecursionDepth)
{
    private static ISessionFactory? sessionFactory;
    private const string DbFile = "test.db";

    [Before(Class)]
    public static void SetUp()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Skip.Test("DbTests are skipped on macOS due to System.Data.SQLite native SQLite.Interop dependency.");
        }

        if (File.Exists(DbFile))
        {
            File.Delete(DbFile);
        }

        Configuration configuration = CreateConfiguration();
        sessionFactory = configuration.BuildSessionFactory();
        
        using (ISession? session = sessionFactory.OpenSession())
        {
            SchemaExport export = new SchemaExport(configuration);
            export.Execute(true, true, false, session.Connection, null);
        }
    }

    [After(Class)]
    public static void TearDown()
    {
        sessionFactory?.Dispose();
        
        if (File.Exists(DbFile))
        {
            File.Delete(DbFile);
        }
    }

    private static Configuration CreateConfiguration()
    {
        return Fluently.Configure()
            .Database(SQLiteConfiguration.Standard
                .ConnectionString($"Data Source={DbFile};Version=3;")
                .ShowSql())
            .Mappings(m => m.FluentMappings
                .Add<EntityMap>()
                .Add<ChildEntityMap>())
            .BuildConfiguration();
    }

    public class Entity
    {
        public virtual int Id { get; set; }
        public virtual string Name { get; set; }
        public virtual IList<ChildEntity> Children { get; set; } = new List<ChildEntity>();
    }

    public class ChildEntity
    {
        public virtual int Id { get; set; }
        public virtual string Name { get; set; }
        public virtual Entity Entity { get; set; }
    }

    public class EntityMap : ClassMap<Entity>
    {
        public EntityMap()
        {
            Table("Entities");
            Id(x => x.Id).GeneratedBy.Identity();
            Map(x => x.Name);
            HasMany(x => x.Children)
                .Cascade.AllDeleteOrphan()
                .Inverse()
                .KeyColumn("EntityId");
        }
    }

    public class ChildEntityMap : ClassMap<ChildEntity>
    {
        public ChildEntityMap()
        {
            Table("ChildEntities");
            Id(x => x.Id).GeneratedBy.Identity();
            Map(x => x.Name);
            References(x => x.Entity)
                .Column("EntityId")
                .Not.Nullable();
        }
    }

    [Test]
    public async Task Test_CloneNHibernateProxy()
    {
        using ISession? session = sessionFactory.OpenSession();
        using ITransaction? transaction = session.BeginTransaction();
        try
        {
            // Arrange
            Entity entity = new Entity 
            { 
                Name = "Test"
            };
                
            ChildEntity child = new ChildEntity 
            { 
                Name = "Child1",
                Entity = entity
            };
                
            entity.Children.Add(child);
                
            session.Save(entity);
            transaction.Commit();
            
            // Act
            Entity? loadedEntity = session.Load<Entity>(entity.Id);
            Entity unproxiedEntity = NHibernateHelper.Unproxy(loadedEntity);
            Entity cloned = unproxiedEntity.DeepClone();

            // Assert
            using (Assert.Multiple())
            {
                await Assert.That(cloned).IsNotNull();
                await Assert.That(cloned).IsNotAssignableTo<INHibernateProxy>();
                await Assert.That(cloned.Id).IsEqualTo(entity.Id);
                await Assert.That(cloned.Name).IsEqualTo("Test");
                await Assert.That(cloned.Children).Count().IsEqualTo(1);
                await Assert.That(cloned.Children[0].Name).IsEqualTo("Child1");

                // Assert
            }
        }
        catch (Exception e)
        {
            transaction.Rollback();
            throw;
        }
    }
}

public static class NHibernateHelper
{
    public static T? Unproxy<T>(T? entity) where T : class
    {
        return entity switch
        {
            null => null,
            INHibernateProxy proxy => (T)proxy.HibernateLazyInitializer.GetImplementation(),
            _ => entity
        };
    }
}