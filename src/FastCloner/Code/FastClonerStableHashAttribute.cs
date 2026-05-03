namespace FastCloner;

/// <summary>
/// Declares that <see cref="object.GetHashCode"/> on this type is stable across deep clones,
/// i.e. a deep-cloned instance will always produce the same hash code as the original.
/// </summary>
/// <remarks>
/// <para>
/// Applying this attribute lets FastCloner skip its runtime probe and use the fast memberwise
/// clone path for hash-based collections (<see cref="System.Collections.Generic.HashSet{T}"/>,
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>, etc.) keyed by this type,
/// which is significantly cheaper than rebuilding the collection.
/// </para>
/// <para>
/// Use this attribute when:
/// <list type="bullet">
/// <item><description>Your <c>GetHashCode</c> is a pure function of the type's fields, OR</description></item>
/// <item><description>Your <c>GetHashCode</c> returns a constant, OR</description></item>
/// <item><description>You know - by construction - that two equal-by-fields instances always hash the same.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Do not</b> apply this attribute if <c>GetHashCode</c> depends on object identity (e.g. uses
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(object)"/>, or hashes a
/// per-instance handle that isn't preserved through cloning). Marking such a type as stable will
/// produce hash collections that lose the ability to look up their own contents after a clone.
/// </para>
/// <para>
/// This attribute is a positive declaration: it marks <i>this</i> type as stable. It does not affect
/// the cloning behavior of the type's members.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [FastClonerStableHash]
/// public sealed class CompositeKey
/// {
///     public int Major { get; }
///     public int Minor { get; }
///     public override int GetHashCode() => HashCode.Combine(Major, Minor);
///     public override bool Equals(object? obj)
///         =&gt; obj is CompositeKey other &amp;&amp; other.Major == Major &amp;&amp; other.Minor == Minor;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class FastClonerStableHashAttribute : Attribute
{
}
