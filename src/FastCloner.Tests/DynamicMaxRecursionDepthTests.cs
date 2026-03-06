using FastCloner.Code;

namespace FastCloner.Tests;

[TestFixture]
public class DynamicMaxRecursionDepthTests
{
    public sealed class ExactNode
    {
        public int Value { get; set; }
        public ExactNode? Child { get; set; }
    }

    public class A
    {
        public List<B> Bs = [];
        public B? Child;
    }

    public class B
    {
        public A? Child;
    }
    
    [OneTimeSetUp]
    public void BaseOneTimeSetUp()
    {
        
    }

    [OneTimeTearDown]
    public void BaseOneTimeTearDown()
    {
        FastCloner.MaxRecursionDepth = 1000;
    }

    [Test]
    public void TestDynamicMaxRecursionDepth()
    {
        // Arrange
        A orig = new A();
        orig.Bs.Add(new B());

        // Act & Assert: MRD = 1
        {
            FastCloner.MaxRecursionDepth = 1;
            A? clone = FastCloner.DeepClone(orig);
            AssertClone(clone);
        }
        
        // Act & Assert: MRD = 2
        {
            FastCloner.MaxRecursionDepth = 2;
            A? clone = FastCloner.DeepClone(orig);
            AssertClone(clone);
        }
        
        // Act & Assert: MRD = 3
        {
            FastCloner.MaxRecursionDepth = 3;
            A? clone = FastCloner.DeepClone(orig);
            AssertClone(clone);
        }

        void AssertClone(A? clone)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That((orig == clone).Dump(), Is.EqualTo("false"), $"Orig should not be the same as clone for depth {FastCloner.MaxRecursionDepth}");
                Assert.That((orig.Bs == clone.Bs).Dump(), Is.EqualTo("false"), $"Orig.Bs should not be the same as clone.Bs for depth {FastCloner.MaxRecursionDepth}");
                Assert.That((orig.Bs.First() == clone.Bs.First()).Dump(), Is.EqualTo("false"), $"Orig.Bs.First() should not be the same as clone.Bs.First() for depth {FastCloner.MaxRecursionDepth}");
            }
        }
    }

    [Test]
    public void TestDeepObjectGraph_1500Levels_WithMRD1000()
    {
        // Arrange
        const int nestLevel = 1500;
        const int maxRecursionDepth = 1000;
        
        A root = new A();
        A currentA = root;
        
        for (int i = 0; i < nestLevel; i++)
        {
            B newB = new B();
            currentA.Child = newB;
            
            if (i < nestLevel - 1)
            {
                A newA = new A();
                newB.Child = newA;
                currentA = newA;
            }
        }
        
        // Act
        FastCloner.MaxRecursionDepth = maxRecursionDepth;
        A? clone = FastCloner.DeepClone(root);
        
        // Assert
        Assert.That(clone, Is.Not.Null);
        Assert.That((root == clone).Dump(), Is.EqualTo("false"));
        
        A? origA = root;
        A? clonedA = clone;
        int level = 0;
        
        while (origA != null && clonedA != null)
        {
            Assert.That((origA == clonedA).Dump(), Is.EqualTo("false"), $"A at level {level} should be different objects");
            
            if (origA.Child != null && clonedA.Child != null)
            {
                Assert.That((origA.Child == clonedA.Child).Dump(), Is.EqualTo("false"), $"B at level {level} should be different objects");
                
                origA = origA.Child.Child;
                clonedA = clonedA.Child.Child;
                level++;
            }
            else
            {
                break;
            }
        }
        
        Assert.That(level, Is.GreaterThan(maxRecursionDepth), $"Verified {level} levels");
    }

    [Test]
    public void TestDeepExactObjectGraph_1500Levels_WithMRD1()
    {
        const int nestLevel = 1500;

        ExactNode root = new ExactNode { Value = 0 };
        ExactNode current = root;

        for (int i = 1; i <= nestLevel; i++)
        {
            ExactNode next = new ExactNode { Value = i };
            current.Child = next;
            current = next;
        }

        FastCloner.MaxRecursionDepth = 1;
        ExactNode? clone = FastCloner.DeepClone(root);

        Assert.That(clone, Is.Not.Null);
        Assert.That(clone, Is.Not.SameAs(root));

        ExactNode? originalCurrent = root;
        ExactNode? cloneCurrent = clone;
        int verifiedLevels = 0;
        while (originalCurrent is not null && cloneCurrent is not null)
        {
            Assert.That(cloneCurrent, Is.Not.SameAs(originalCurrent), $"Node at level {verifiedLevels} should be cloned");
            Assert.That(cloneCurrent.Value, Is.EqualTo(originalCurrent.Value), $"Value at level {verifiedLevels} should match");

            originalCurrent = originalCurrent.Child;
            cloneCurrent = cloneCurrent.Child;
            verifiedLevels++;
        }

        Assert.That(verifiedLevels, Is.EqualTo(nestLevel + 1));
        Assert.That(cloneCurrent, Is.Null);
        Assert.That(originalCurrent, Is.Null);
    }

    [Test]
    public void Internal_CloneClassInternalExact_Should_Reset_CallDepth_After_WorkList_Switch()
    {
        ExactNode root = new ExactNode
        {
            Value = 1,
            Child = new ExactNode
            {
                Value = 2,
                Child = new ExactNode { Value = 3 }
            }
        };

        FastCloner.MaxRecursionDepth = 1;
        FastCloneState state = FastCloneState.Rent();
        try
        {
            ExactNode? clone = FastClonerGenerator.CloneClassInternalExact(root, state);

            Assert.That(clone, Is.Not.Null);
            Assert.That(state.UseWorkList, Is.True);
            Assert.That(state.CurrentDepth, Is.EqualTo(0));
        }
        finally
        {
            FastCloneState.Return(state);
        }
    }
}