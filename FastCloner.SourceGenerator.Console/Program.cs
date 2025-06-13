using FastCloner.SourceGenerator.Shared;

namespace FastCloner.SourceGenerator.Console;
using System;

[FastClonerClonable]
public partial class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Hobbies { get; set; }
}

class Program
{
    static void Main(string[] args)
    {
        Person person = new Person
        {
            Name = "John",
            Age = 30,
            Hobbies = new List<string> { "Reading", "Gaming" }
        };

        /*var clone = PersonClone.Clone(person);

        Console.WriteLine($"Original: {person.Name}, {person.Age}");
        Console.WriteLine($"Clone: {clone.Name}, {clone.Age}");*/
    }
}