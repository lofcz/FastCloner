using System.Linq;
using FastCloner.SourceGenerator.Shared;
using NUnit.Framework;

namespace FastCloner.Tests;

// External derived types (simulating types from another assembly or not auto-discovered)
public class ExternalDog : AbstractClassTests.Pet
{
    public string? Breed { get; set; }
}

public class ExternalCat : AbstractClassTests.Pet
{
    public string? FurColor { get; set; }
}

public class ExternalBird : AbstractClassTests.Pet
{
    public double Wingspan { get; set; }
    public bool CanFly { get; set; }
}

[TestFixture]
[SourceGeneratorCompatible]
public class AbstractClassTests
{
    #region Test Classes - Abstract Base with [FastClonerInclude]
    
    /// <summary>
    /// Abstract base class that uses [FastClonerInclude] to register external derived types.
    /// Auto-discovery is disabled to ensure only explicitly included types are used.
    /// </summary>
    [FastClonerClonable]
    [FastClonerDisableAutoDiscovery]
    [FastClonerInclude(typeof(ExternalDog), typeof(ExternalCat), typeof(ExternalBird))]
    public abstract class Pet
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    #endregion

    #region Test Classes - Abstract Base with Same Assembly Derived Types
    
    [FastClonerClonable]
    public abstract class Animal
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    public class Dog : Animal
    {
        public string? Breed { get; set; }
        public bool IsTrained { get; set; }
    }

    public class Cat : Animal
    {
        public string? Color { get; set; }
        public bool IsIndoor { get; set; }
    }

    #endregion

    #region Test Classes - Abstract Base with Collections
    
    [FastClonerClonable]
    public abstract class Vehicle
    {
        public string? Brand { get; set; }
        public List<string>? Features { get; set; }
    }

    public class Car : Vehicle
    {
        public int NumberOfDoors { get; set; }
        public bool IsElectric { get; set; }
    }

    public class Motorcycle : Vehicle
    {
        public bool HasSidecar { get; set; }
    }

    #endregion

    #region Test Classes - Multi-level Inheritance
    
    [FastClonerClonable]
    public abstract class Shape
    {
        public string? Name { get; set; }
    }

    public abstract class Polygon : Shape
    {
        public int NumberOfSides { get; set; }
    }

    public class Triangle : Polygon
    {
        public double Base { get; set; }
        public double Height { get; set; }
        
        public Triangle()
        {
            NumberOfSides = 3;
        }
    }

    public class Rectangle : Polygon
    {
        public double Width { get; set; }
        public double Length { get; set; }
        
        public Rectangle()
        {
            NumberOfSides = 4;
        }
    }

    #endregion

    #region Test Classes - Abstract with Nested Complex Types
    
    [FastClonerClonable]
    public abstract class Employee
    {
        public string? Name { get; set; }
        public Address? HomeAddress { get; set; }
    }

    public class Manager : Employee
    {
        public List<string>? DirectReports { get; set; }
        public string? Department { get; set; }
    }

    public class Developer : Employee
    {
        public List<string>? ProgrammingLanguages { get; set; }
        public string? Team { get; set; }
    }

    public class Address
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? ZipCode { get; set; }
    }

    #endregion

    #region Tests - Basic Abstract Class Cloning
    
    [Test]
    [SourceGeneratorCompatible]
    public void Abstract_Dog_Should_Clone_Correctly()
    {
        // Arrange
        Dog dog = new Dog
        {
            Name = "Buddy",
            Age = 5,
            Breed = "Golden Retriever",
            IsTrained = true
        };
        
        // Act - Clone via abstract base type
        Animal animal = dog;
        Animal? clone = animal.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Dog>());
        Assert.That(clone, Is.Not.SameAs(dog));
        
        var clonedDog = (Dog)clone!;
        Assert.That(clonedDog.Name, Is.EqualTo("Buddy"));
        Assert.That(clonedDog.Age, Is.EqualTo(5));
        Assert.That(clonedDog.Breed, Is.EqualTo("Golden Retriever"));
        Assert.That(clonedDog.IsTrained, Is.True);
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Abstract_Cat_Should_Clone_Correctly()
    {
        // Arrange
        Cat cat = new Cat
        {
            Name = "Whiskers",
            Age = 3,
            Color = "Orange",
            IsIndoor = true
        };
        
        // Act - Clone via abstract base type
        Animal animal = cat;
        Animal? clone = animal.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Cat>());
        Assert.That(clone, Is.Not.SameAs(cat));
        
        var clonedCat = (Cat)clone!;
        Assert.That(clonedCat.Name, Is.EqualTo("Whiskers"));
        Assert.That(clonedCat.Age, Is.EqualTo(3));
        Assert.That(clonedCat.Color, Is.EqualTo("Orange"));
        Assert.That(clonedCat.IsIndoor, Is.True);
    }

    #endregion

    #region Tests - Abstract with Collections
    
    [Test]
    [SourceGeneratorCompatible]
    public void Abstract_Vehicle_With_Collection_Should_Deep_Clone()
    {
        // Arrange
        Car car = new Car
        {
            Brand = "Tesla",
            Features = ["Autopilot", "Electric", "Touchscreen"],
            NumberOfDoors = 4,
            IsElectric = true
        };
        
        // Act
        Vehicle vehicle = car;
        Vehicle? clone = vehicle.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Car>());
        Assert.That(clone!.Features, Is.Not.SameAs(car.Features));
        
        var clonedCar = (Car)clone;
        Assert.That(clonedCar.Brand, Is.EqualTo("Tesla"));
        Assert.That(clonedCar.NumberOfDoors, Is.EqualTo(4));
        Assert.That(clonedCar.IsElectric, Is.True);
        Assert.That(clonedCar.Features!.Count, Is.EqualTo(3));
        
        // Verify deep clone - modify original
        car.Features![0] = "Manual";
        Assert.That(clonedCar.Features[0], Is.EqualTo("Autopilot")); // Clone unchanged
    }

    #endregion

    #region Tests - Multi-level Inheritance
    
    [Test]
    [SourceGeneratorCompatible]
    public void MultiLevel_Triangle_Should_Clone_Via_Shape()
    {
        // Arrange
        Triangle triangle = new Triangle
        {
            Name = "Equilateral",
            Base = 10.0,
            Height = 8.66
        };
        
        // Act - Clone via most abstract type
        Shape shape = triangle;
        Shape? clone = shape.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Triangle>());
        
        var clonedTriangle = (Triangle)clone!;
        Assert.That(clonedTriangle.Name, Is.EqualTo("Equilateral"));
        Assert.That(clonedTriangle.NumberOfSides, Is.EqualTo(3));
        Assert.That(clonedTriangle.Base, Is.EqualTo(10.0));
        Assert.That(clonedTriangle.Height, Is.EqualTo(8.66));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void MultiLevel_Rectangle_Should_Clone_Via_Shape()
    {
        // Arrange
        Rectangle rectangle = new Rectangle
        {
            Name = "Square",
            Width = 5.0,
            Length = 5.0
        };
        
        // Act
        Shape shape = rectangle;
        Shape? clone = shape.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Rectangle>());
        
        var clonedRect = (Rectangle)clone!;
        Assert.That(clonedRect.Name, Is.EqualTo("Square"));
        Assert.That(clonedRect.NumberOfSides, Is.EqualTo(4));
        Assert.That(clonedRect.Width, Is.EqualTo(5.0));
        Assert.That(clonedRect.Length, Is.EqualTo(5.0));
    }

    #endregion

    #region Tests - Abstract with Complex Nested Types
    
    [Test]
    [SourceGeneratorCompatible]
    public void Abstract_Employee_With_Complex_Types_Should_Deep_Clone()
    {
        // Arrange
        Manager manager = new Manager
        {
            Name = "Alice",
            HomeAddress = new Address
            {
                Street = "123 Main St",
                City = "Seattle",
                ZipCode = "98101"
            },
            DirectReports = ["Bob", "Charlie", "Diana"],
            Department = "Engineering"
        };
        
        // Act
        Employee employee = manager;
        Employee? clone = employee.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Manager>());
        
        var clonedManager = (Manager)clone!;
        Assert.That(clonedManager.Name, Is.EqualTo("Alice"));
        Assert.That(clonedManager.Department, Is.EqualTo("Engineering"));
        
        // Verify deep clone of nested object
        Assert.That(clonedManager.HomeAddress, Is.Not.Null);
        Assert.That(clonedManager.HomeAddress, Is.Not.SameAs(manager.HomeAddress));
        Assert.That(clonedManager.HomeAddress!.Street, Is.EqualTo("123 Main St"));
        Assert.That(clonedManager.HomeAddress.City, Is.EqualTo("Seattle"));
        
        // Verify deep clone of list
        Assert.That(clonedManager.DirectReports, Is.Not.Null);
        Assert.That(clonedManager.DirectReports, Is.Not.SameAs(manager.DirectReports));
        Assert.That(clonedManager.DirectReports!.Count, Is.EqualTo(3));
        
        // Verify independence
        manager.HomeAddress!.City = "Portland";
        Assert.That(clonedManager.HomeAddress.City, Is.EqualTo("Seattle")); // Clone unchanged
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Abstract_Developer_Should_Clone_Correctly()
    {
        // Arrange
        Developer developer = new Developer
        {
            Name = "Bob",
            HomeAddress = new Address
            {
                Street = "456 Oak Ave",
                City = "San Francisco",
                ZipCode = "94102"
            },
            ProgrammingLanguages = ["C#", "Python", "TypeScript"],
            Team = "Backend"
        };
        
        // Act
        Employee employee = developer;
        Employee? clone = employee.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Developer>());
        
        var clonedDev = (Developer)clone!;
        Assert.That(clonedDev.Name, Is.EqualTo("Bob"));
        Assert.That(clonedDev.Team, Is.EqualTo("Backend"));
        Assert.That(clonedDev.ProgrammingLanguages!.Count, Is.EqualTo(3));
        Assert.That(clonedDev.ProgrammingLanguages, Does.Contain("C#"));
    }

    #endregion

    #region Tests - [FastClonerInclude] for External Derived Types

    [Test]
    [SourceGeneratorCompatible]
    public void FastClonerInclude_ExternalDog_Should_Clone()
    {
        // Arrange
        ExternalDog dog = new ExternalDog
        {
            Name = "Rex",
            Age = 4,
            Breed = "German Shepherd"
        };
        
        // Act - Clone via abstract base type
        Pet pet = dog;
        Pet? clone = pet.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<ExternalDog>());
        Assert.That(clone, Is.Not.SameAs(dog));
        
        var clonedDog = (ExternalDog)clone!;
        Assert.That(clonedDog.Name, Is.EqualTo("Rex"));
        Assert.That(clonedDog.Age, Is.EqualTo(4));
        Assert.That(clonedDog.Breed, Is.EqualTo("German Shepherd"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void FastClonerInclude_ExternalCat_Should_Clone()
    {
        // Arrange
        ExternalCat cat = new ExternalCat
        {
            Name = "Mittens",
            Age = 2,
            FurColor = "Calico"
        };
        
        // Act
        Pet pet = cat;
        Pet? clone = pet.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<ExternalCat>());
        
        var clonedCat = (ExternalCat)clone!;
        Assert.That(clonedCat.Name, Is.EqualTo("Mittens"));
        Assert.That(clonedCat.Age, Is.EqualTo(2));
        Assert.That(clonedCat.FurColor, Is.EqualTo("Calico"));
    }

    [Test]
    [SourceGeneratorCompatible]
    public void FastClonerInclude_ExternalBird_Should_Clone()
    {
        // Arrange
        ExternalBird bird = new ExternalBird
        {
            Name = "Tweety",
            Age = 1,
            Wingspan = 0.3,
            CanFly = true
        };
        
        // Act
        Pet pet = bird;
        Pet clone = pet.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<ExternalBird>());
        
        var clonedBird = (ExternalBird)clone!;
        Assert.That(clonedBird.Name, Is.EqualTo("Tweety"));
        Assert.That(clonedBird.Age, Is.EqualTo(1));
        Assert.That(clonedBird.Wingspan, Is.EqualTo(0.3));
        Assert.That(clonedBird.CanFly, Is.True);
    }

    [Test]
    [SourceGeneratorCompatible]
    public void FastClonerInclude_Multiple_Types_Independence()
    {
        // Arrange - create instances of all included types
        ExternalDog dog = new ExternalDog { Name = "Buddy", Age = 3, Breed = "Labrador" };
        ExternalCat cat = new ExternalCat { Name = "Whiskers", Age = 5, FurColor = "Orange" };
        ExternalBird bird = new ExternalBird { Name = "Polly", Age = 2, Wingspan = 0.5, CanFly = true };
        
        // Act - Clone all via abstract base
        Pet[] pets = [dog, cat, bird];
        Pet?[] clones = pets.Select(p => p.FastDeepClone()).ToArray();
        
        // Assert - Each clone is correct type and independent
        Assert.That(clones[0], Is.TypeOf<ExternalDog>());
        Assert.That(clones[1], Is.TypeOf<ExternalCat>());
        Assert.That(clones[2], Is.TypeOf<ExternalBird>());
        
        // Verify independence - modify originals
        dog.Name = "Changed";
        cat.FurColor = "Black";
        bird.CanFly = false;
        
        Assert.That(((ExternalDog)clones[0]!).Name, Is.EqualTo("Buddy"));
        Assert.That(((ExternalCat)clones[1]!).FurColor, Is.EqualTo("Orange"));
        Assert.That(((ExternalBird)clones[2]!).CanFly, Is.True);
    }

    #endregion

    #region Tests - Null Handling
    
    [Test]
    [SourceGeneratorCompatible]
    public void Null_Abstract_Should_Return_Null()
    {
        // Arrange
        Animal? animal = null;
        
        // Act
        Animal? clone = animal.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Null);
    }

    [Test]
    [SourceGeneratorCompatible]
    public void Abstract_With_Null_Properties_Should_Clone()
    {
        // Arrange
        Dog dog = new Dog
        {
            Name = null,
            Age = 2,
            Breed = null,
            IsTrained = false
        };
        
        // Act
        Animal animal = dog;
        Animal? clone = animal.FastDeepClone();
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.TypeOf<Dog>());
        
        var clonedDog = (Dog)clone!;
        Assert.That(clonedDog.Name, Is.Null);
        Assert.That(clonedDog.Age, Is.EqualTo(2));
        Assert.That(clonedDog.Breed, Is.Null);
        Assert.That(clonedDog.IsTrained, Is.False);
    }

    #endregion
}

