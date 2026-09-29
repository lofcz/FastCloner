using System;

namespace FastCloner.SourceGenerator.Shared;

/// <summary>
/// Controls identity preservation behavior during cloning.
/// <br/><br/>
/// Identity preservation ensures that if the same object instance appears multiple times
/// in the source graph (e.g., two properties pointing to the same object, or the same
/// object appearing twice in a collection), the cloned graph maintains this relationship
/// (both cloned references point to the same cloned instance).
/// <br/><br/>
/// By default, identity preservation is disabled for performance. Circular reference
/// detection is always enabled.
/// <br/><br/>
/// This attribute can be applied to:<br/>
/// - Classes/structs: Controls identity preservation for the entire type's subgraph<br/>
/// - Properties/fields: Controls identity preservation for that specific member's subgraph
/// <br/><br/>
/// Member-level attributes override type-level attributes, which override the default behaviour.
/// <br/><br/>
/// <c>[FastClonerPreserveIdentity(false)]</c> suppresses alias preservation, but it never disables
/// cycle detection. The opt-out is a view over the same clone operation: objects whose clones are
/// still being constructed stay visible through it, so an active cycle — including a cycle that
/// passes through the opted-out member, or one that points back at the collection, array or
/// dictionary the member came from — resolves to the clone under construction instead of recursing.
/// Only completed aliases are dropped, so two occurrences of the same source object under an opt-out
/// still clone independently. Because the opt-out is member-precise rather than type-wide, an
/// unrelated cycle-capable member of the containing type does not re-enable aliasing inside the
/// opted-out member's subgraph.
/// <br/><br/>
/// The override covers every member kind generated code can clone: clonable members, implicit
/// (unannotated) POCOs, collections, arrays, multidimensional arrays and dictionaries, including
/// non-public members on .NET 8 and later, which are cloned through generated accessors. It does not
/// reach members the generator cannot clone itself — <c>object</c>, type parameters, and any other
/// member handled by the runtime cloner — nor non-public members on target frameworks below .NET 8,
/// which are cloned through the runtime bridge. Those paths track references on the runtime engine's
/// own terms. Types registered in a <c>FastClonerContext</c> are cloned by the context's own generated
/// cloners, which honour the override in the same way.
/// <br/><br/>
/// Pre-existing shapes are outside the override, because their containers do not exist yet while
/// their contents are cloned:
/// <list type="bullet">
/// <item>a getter-only collection or dictionary member, whose contents are cloned into an intermediate
/// collection and then copied into the instance the member's getter returns (a back-reference resolves
/// to that intermediate clone rather than to the collection the caller sees);</item>
/// <item>a <c>ReadOnlyCollection&lt;T&gt;</c> or <c>ReadOnlyDictionary&lt;TKey,TValue&gt;</c> whose
/// element or value refers back to the wrapper (the wrapper is created - and can only be registered -
/// after its contents have been cloned, so the back-reference resolves to a second wrapper);</item>
/// <item>an immutable collection or immutable dictionary whose element refers back to the container
/// (the container is built from its cloned elements, so the back-reference cannot be closed and
/// resolves to a second container);</item>
/// <item>an implicit (unannotated) POCO that participates in a cycle through itself (its instance is
/// built with an object initializer, so members are cloned before the instance can be registered).</item>
/// </list>
/// Mutable collections, arrays and dictionaries reached through an opted-out member do close such
/// back-references. None of the shapes above is affected by this attribute: they reproduce on
/// <c>next</c> without it, and closing them needs a construction/fixup design (build the contents,
/// then patch the back-references, or populate the target container directly) that generated code does
/// not currently use.
/// </summary>
/// <example>
/// <code>
/// // Enable identity preservation (default when attribute is present)
/// [FastClonerPreserveIdentity]
/// public class MyClass { }
/// 
/// // Explicitly enable
/// [FastClonerPreserveIdentity(true)]
/// public class MyClass { }
/// 
/// // Disable identity preservation for a member
/// [FastClonerPreserveIdentity(false)]
/// public List&lt;Item&gt; Items { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class FastClonerPreserveIdentityAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether identity preservation is enabled.
    /// Default is true when the attribute is applied.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Enables identity preservation for this type or member's subgraph.
    /// </summary>
    public FastClonerPreserveIdentityAttribute()
    {
    }

    /// <summary>
    /// Controls identity preservation for this type or member's subgraph.
    /// </summary>
    /// <param name="enabled">True to enable identity preservation, false to disable.</param>
    public FastClonerPreserveIdentityAttribute(bool enabled)
    {
        Enabled = enabled;
    }
}
