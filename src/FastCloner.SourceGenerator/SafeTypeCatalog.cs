using System;
using System.Collections.Generic;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Central list of type names that are considered "Safe" (immutable or simple value types)
/// across both the Runtime library and Source Generator.
/// </summary>
internal static class SafeTypeCatalog
{
    public static readonly HashSet<string> SafeTypeNames = new HashSet<string>
    {
        // Primitives
        "System.Byte", 
        "System.SByte", 
        "System.Int16", 
        "System.UInt16",
        "System.Int32", 
        "System.UInt32", 
        "System.Int64", 
        "System.UInt64",
        "System.Single", 
        "System.Double", 
        "System.Decimal",
        "System.String", 
        "System.Char", 
        "System.Boolean",
        "System.Guid", 
        "System.IntPtr", 
        "System.UIntPtr", 
        "System.DBNull",
        
        // Time-related types
        "System.TimeSpan", 
        "System.DateTime", 
        "System.DateTimeOffset",
        "System.TimeZoneInfo",
        
        // Modern .NET types (Strings are safe to use even in older targets)
        "System.DateOnly",
        "System.TimeOnly",
        "System.Half",
        "System.Int128",
        "System.UInt128",
        "System.Numerics.Complex",
        "System.Text.Rune",
        "System.Range",
        "System.Index",
        
        // Comparers (often singletons or stateless)
        "System.StringComparer", // Base class?
        // Note: Specific comparers are often internal or nested types in runtime,
        // but we can list public ones if needed.
        // FastClonerSafeTypes.cs handles specific instances like StringComparer.Ordinal.GetType()
        // which might be private classes. We can't easily list them by string name here for SG.
        // BUT SG generally sees the abstract/base type or public type.
        
        // Threading/Cancellation types (effectively immutable/singletons)
        "System.Threading.CancellationToken",
        "System.Threading.CancellationTokenSource",
    };

    /// <summary>
    /// Generic type definitions that should NOT be deep cloned.
    /// These types represent behavior, lazy evaluation, weak references, or async operations
    /// where deep cloning would be semantically incorrect.
    /// </summary>
    public static readonly HashSet<string> DoNotCloneGenericTypes = new HashSet<string>
    {
        // Behavioral/delegate types - deep cloning makes no semantic sense
        "System.Func`1",
        "System.Func`2",
        "System.Func`3",
        "System.Func`4",
        "System.Func`5",
        "System.Func`6",
        "System.Func`7",
        "System.Func`8",
        "System.Func`9",
        "System.Func`10",
        "System.Func`11",
        "System.Func`12",
        "System.Func`13",
        "System.Func`14",
        "System.Func`15",
        "System.Func`16",
        "System.Func`17",
        "System.Action`1",
        "System.Action`2",
        "System.Action`3",
        "System.Action`4",
        "System.Action`5",
        "System.Action`6",
        "System.Action`7",
        "System.Action`8",
        "System.Action`9",
        "System.Action`10",
        "System.Action`11",
        "System.Action`12",
        "System.Action`13",
        "System.Action`14",
        "System.Action`15",
        "System.Action`16",
        "System.Predicate`1",
        "System.Comparison`1",
        "System.Converter`2",
        "System.EventHandler`1",
        
        // Lazy evaluation - should preserve reference, not clone
        "System.Lazy`1",
        "System.Lazy`2",
        
        // Weak references - should preserve reference semantics
        "System.WeakReference`1",
        
        // Async types - deep cloning makes no sense
        "System.Threading.Tasks.Task`1",
        "System.Threading.Tasks.ValueTask`1",
        "System.Threading.Tasks.TaskCompletionSource`1",
        
        // Expression trees - represent code, not data
        "System.Linq.Expressions.Expression`1",
    };

    /// <summary>
    /// Non-generic delegate and async types that should NOT be deep cloned.
    /// </summary>
    public static readonly HashSet<string> DoNotCloneTypes = new HashSet<string>
    {
        // Non-generic delegates
        "System.Action",
        "System.Delegate",
        "System.MulticastDelegate",
        "System.EventHandler",
        
        // Non-generic weak reference
        "System.WeakReference",
        
        // Non-generic async types
        "System.Threading.Tasks.Task",
        "System.Threading.Tasks.ValueTask",
        
        // Expression base type
        "System.Linq.Expressions.Expression",
        "System.Linq.Expressions.LambdaExpression",
    };

    // currently never used
    /// <summary>
    /// Generic ref struct types that cannot be cloned (can't be boxed for state tracking).
    /// These must be detected by IsRefLikeType as well, but we list known ones for documentation.
    /// </summary>
    public static readonly HashSet<string> RefStructGenericTypes = new HashSet<string>
    {
        "System.Span`1",
        "System.ReadOnlySpan`1",
        "System.Memory`1",
        "System.ReadOnlyMemory`1",
    };
}
