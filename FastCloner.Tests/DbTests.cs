using System.Data.Common;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using FluentNHibernate.Mapping;
using NHibernate.Cfg;
using NHibernate.Cfg.MappingSchema;
using NHibernate.Collection;
using NHibernate.Mapping.ByCode;
using NHibernate.Tool.hbm2ddl;

namespace FastCloner.Tests;

using NHibernate;
using NHibernate.Proxy;

[TestFixture]
public class DbTests
{
    private ISessionFactory sessionFactory;
    private const string DbFile = "test.db";

    [OneTimeSetUp]
    public void SetUp()
    {
        // Smažeme existující databázi, pokud existuje
        if (File.Exists(DbFile))
        {
            File.Delete(DbFile);
        }

        Configuration configuration = CreateConfiguration();
        sessionFactory = configuration.BuildSessionFactory();

        // Vytvoříme schéma databáze
        using (ISession? session = sessionFactory.OpenSession())
        {
            SchemaExport export = new SchemaExport(configuration);
            export.Execute(true, true, false, session.Connection, null);
        }
    }

    [OneTimeTearDown]
    public void TearDown()
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
            Table("Entities"); // Změněno z "Entity"
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
            Table("ChildEntities"); // Změněno z "ChildEntity"
            Id(x => x.Id).GeneratedBy.Identity();
            Map(x => x.Name);
            References(x => x.Entity)
                .Column("EntityId")
                .Not.Nullable();
        }
    }

    [Test]
    public void Test_CloneNHibernateProxy()
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
            Assert.Multiple(() =>
            {
                Assert.That(cloned, Is.Not.Null);
                Assert.That(cloned, Is.Not.InstanceOf<INHibernateProxy>());
                Assert.That(cloned.Id, Is.EqualTo(entity.Id));
                Assert.That(cloned.Name, Is.EqualTo("Test"));
                Assert.That(cloned.Children, Has.Count.EqualTo(1));
                Assert.That(cloned.Children[0].Name, Is.EqualTo("Child1"));
            });
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
    public static T Unproxy<T>(T entity) where T : class
    {
        if (entity == null)
            return null;

        // Pokud je to proxy objekt
        if (entity is INHibernateProxy proxy)
        {
            return (T)proxy.HibernateLazyInitializer.GetImplementation();
        }

        return entity;
    }
}