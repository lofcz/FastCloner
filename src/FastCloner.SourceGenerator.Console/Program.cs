using FastCloner.SourceGenerator.Shared;

namespace FastCloner.SourceGenerator.Console;
using System;

/*[FastClonerClonable]
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Hobbies { get; set; }
}*/

[FastClonerClonable]
[FastClonerSimulateNoRuntime]
public class GenericClassWithConstraint<T>
{
    public T Value { get; set; }
}

public class SampleUnannotatedClass
{
    public List<string> StringList { get; set; }
}

[FastClonerClonable]
public class GenericClassWithInclude<T>
{
    public T Value { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        var myTest = new GenericClassWithConstraint<Dictionary<string, SampleUnannotatedClass>>();
        
        var original = new GenericClassWithConstraint<List<int>> { Value = new List<int> { 1, 2, 3 } };
        var clone = original.FastDeepClone();
        
        /*Person person = new Person
        {
            Name = "John",
            Age = 30,
            Hobbies = new List<string> { "Reading", "Gaming" }
        };*/

        /*var clone = PersonClone.Clone(person);

        Console.WriteLine($"Original: {person.Name}, {person.Age}");
        Console.WriteLine($"Clone: {clone.Name}, {clone.Age}");*/
    }
}