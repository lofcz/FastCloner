using FastCloner.Code;
using System.Threading.Tasks;

namespace FastCloner.Tests;
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
    
    [After(Test)]
    public void ResetMaxRecursionDepth()
    {
        FastCloner.MaxRecursionDepth = 1000;
    }

    [Test]
    public async Task TestDynamicMaxRecursionDepth()
    {
        // Arrange
        A orig = new A();
        orig.Bs.Add(new B());

        // Act & Assert: MRD = 1
        {
            FastCloner.MaxRecursionDepth = 1;
            A? clone = FastCloner.DeepClone(orig);
            await AssertClone(clone);
        }
        
        // Act & Assert: MRD = 2
        {
            FastCloner.MaxRecursionDepth = 2;
            A? clone = FastCloner.DeepClone(orig);
            await AssertClone(clone);
        }
        
        // Act & Assert: MRD = 3
        {
            FastCloner.MaxRecursionDepth = 3;
            A? clone = FastCloner.DeepClone(orig);
            await AssertClone(clone);
        }

        async Task AssertClone(A? clone)
        {
            await Assert.That((orig == clone).Dump()).IsEqualTo("false").Because($"Orig should not be the same as clone for depth {FastCloner.MaxRecursionDepth}");
            await Assert.That((orig.Bs == clone.Bs).Dump()).IsEqualTo("false").Because($"Orig.Bs should not be the same as clone.Bs for depth {FastCloner.MaxRecursionDepth}");
            await Assert.That((orig.Bs.First() == clone.Bs.First()).Dump()).IsEqualTo("false").Because($"Orig.Bs.First() should not be the same as clone.Bs.First() for depth {FastCloner.MaxRecursionDepth}");
        }
    }

    [Test]
    public async Task TestDeepObjectGraph_1500Levels_WithMRD1000()
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
        await Assert.That(clone).IsNotNull();
        await Assert.That((root == clone).Dump()).IsEqualTo("false");

        A? origA = root;
        A? clonedA = clone;
        int level = 0;
        
        while (origA != null && clonedA != null)
        {
            await Assert.That((origA == clonedA).Dump()).IsEqualTo("false").Because($"A at level {level} should be different objects");

            if (origA.Child != null && clonedA.Child != null)
            {
                await Assert.That((origA.Child == clonedA.Child).Dump()).IsEqualTo("false").Because($"B at level {level} should be different objects");

                origA = origA.Child.Child;
                clonedA = clonedA.Child.Child;
                level++;
            }
            else
            {
                break;
            }
        }
        
        await Assert.That(level).IsGreaterThan(maxRecursionDepth).Because($"Verified {level} levels");
    }

    [Test]
    public async Task TestDeepExactObjectGraph_1500Levels_WithMRD1()
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

        await Assert.That(clone).IsNotNull();
        await Assert.That(clone).IsNotSameReferenceAs(root);

        ExactNode? originalCurrent = root;
        ExactNode? cloneCurrent = clone;
        int verifiedLevels = 0;
        while (originalCurrent is not null && cloneCurrent is not null)
        {
            await Assert.That(cloneCurrent).IsNotSameReferenceAs(originalCurrent).Because($"Node at level {verifiedLevels} should be cloned");
            await Assert.That(cloneCurrent.Value).IsEqualTo(originalCurrent.Value).Because($"Value at level {verifiedLevels} should match");

            originalCurrent = originalCurrent.Child;
            cloneCurrent = cloneCurrent.Child;
            verifiedLevels++;
        }

        await Assert.That(verifiedLevels).IsEqualTo(nestLevel + 1);
        await Assert.That(cloneCurrent).IsNull();
        await Assert.That(originalCurrent).IsNull();
    }

    [Test]
    public async Task Internal_CloneClassInternal_Should_Reset_CallDepth_After_WorkList_Switch()
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

        int previousMaxRecursionDepth = FastCloner.MaxRecursionDepth;
        FastCloner.MaxRecursionDepth = 1;
        FastCloneState state = FastCloneState.Rent();
        try
        {
            ExactNode? clone = (ExactNode?)FastClonerGenerator.CloneClassInternal(root, state);

            await Assert.That(clone).IsNotNull();
            await Assert.That(state.UseWorkList).IsTrue();
            await Assert.That(state.CurrentDepth).IsEqualTo(0);
        }
        finally
        {
            FastCloneState.Return(state);
            FastCloner.MaxRecursionDepth = previousMaxRecursionDepth;
        }
    }
}