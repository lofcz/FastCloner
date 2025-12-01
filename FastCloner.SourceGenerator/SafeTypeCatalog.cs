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
    };
}
