namespace FastCloner.Code;

/// <summary>
/// Marks a field or property for shallow cloning instead of deep cloning.
/// The member reference will be copied directly without recursively cloning its contents.
/// This is useful for parent references, shared state, or when deep cloning would cause issues.
/// </summary>
/// <example>
/// <code>
/// public class Node
/// {
///     [FastClonerShallow]
///     public ParentObject Parent { get; set; }  // Reference copied, not deep cloned
///     
///     public Foo OtherProperty { get; set; }    // Deep cloned normally
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class FastClonerShallowAttribute : Attribute
{
}
