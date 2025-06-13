using FastCloner.SourceGenerator;
using FastCloner.SourceGenerator.Analyzers;
using FastCloner.SourceGenerator.Shared;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace FastCloner.Tests;

[TestFixture(Explicit = true)]
public class SourceGeneratorTests
{
    [FastClonerClonable]
    public partial class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public List<string> Hobbies { get; set; }
    }
    
    [Test]
    public void Sample()
    {
        // Arrange
        Person person = new Person();
        
        // Act
        Person copy = person.DeepClone();
        
        // Assert
        Assert.That(copy.Age, Is.EqualTo(person.Age));
    }

    [Test]
    public async Task Analyzer1()
    {
        // [|text|]: indicates that a diagnostic is reported for text. By default, this form may only be used for testing analyzers with exactly one DiagnosticDescriptor provided by DiagnosticAnalyzer.SupportedDiagnostics.
        // {|ExpectedDiagnosticId:text|}: indicates that a diagnostic with Id ExpectedDiagnosticId is reported for text.
        
        CSharpAnalyzerTest<FastClonerClonableAnalyzer, DefaultVerifier> context = new CSharpAnalyzerTest<FastClonerClonableAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            
            TestCode = """
                       using System;
                       using FastCloner.SourceGenerator;

                       namespace MyTestCode
                       {
                           [FastClonerClonable]
                           public class {|#0:Person|}
                           {
                           }
                       }
                       """
        };

        await context.RunAsync();
    }
}