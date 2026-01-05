using System.Collections.Generic;
using System.Text;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Maintains state during the code generation process.
/// </summary>
internal sealed class CloneGeneratorContext
{
    public TypeModel Model { get; }
    public StringBuilder Source { get; } = new();
    
    private readonly Dictionary<string, string> _typeNameToMethodName;
    private readonly HashSet<string> _neededHelperMethods;
    private readonly Queue<string> _pendingHelperMethods = new();
    private readonly Dictionary<string, MemberModel> _typeNameToMemberModel = new();
    private readonly Dictionary<string, TypeModel> _implicitTypeModels = new();
    private readonly Dictionary<string, TypeModel> _derivedTypeHelpers = new();
    private readonly Dictionary<string, int> _helperUsageCounts = new();

    public bool NeedsStateClass { get; set; }
    public bool NeedsClonerClass { get; set; }
    public bool UseStaticMethods { get; set; } = true;
    
    public bool CanHaveCircularReferences { get; set; }
    public bool IsFastClonerAvailable { get; }
    
    private readonly Dictionary<string, bool> _circularReferenceOverrides = new();

    public CloneGeneratorContext(TypeModel model, Dictionary<string, string>? sharedMethodNames = null, HashSet<string>? sharedNeededHelpers = null)
    {
        Model = model;
        CanHaveCircularReferences = model.CanHaveCircularReferences;
        IsFastClonerAvailable = model.IsFastClonerAvailable;
        
        _typeNameToMethodName = sharedMethodNames ?? new Dictionary<string, string>();
        _neededHelperMethods = sharedNeededHelpers ?? [];

        foreach (TypeModel? related in model.RelatedTypes)
        {
            _implicitTypeModels[related.FullyQualifiedName] = related;
        }
        
        foreach (MemberModel nested in model.NestedTypes)
        {
            if (!_typeNameToMemberModel.ContainsKey(nested.TypeFullName))
            {
                _typeNameToMemberModel[nested.TypeFullName] = nested;
            }
        }
    }

    public void SetCircularReferenceOverride(string typeName, bool needsState)
    {
        _circularReferenceOverrides[typeName] = needsState;
    }

    public bool NeedsCircularState(string typeName, bool defaultFromModel)
    {
        if (_circularReferenceOverrides.TryGetValue(typeName, out bool overrideValue))
        {
            return overrideValue;
        }
        return defaultFromModel;
    }

    public bool HasPendingHelperMethods => _pendingHelperMethods.Count > 0;

    public string DequeuePendingHelperMethod() => _pendingHelperMethods.Dequeue();

    public bool TryGetImplicitTypeModel(string typeName, out TypeModel model)
    {
        return _implicitTypeModels.TryGetValue(typeName, out model);
    }

    public bool TryGetMemberModel(string typeName, out MemberModel model)
    {
        return _typeNameToMemberModel.TryGetValue(typeName, out model);
    }

    public string GetMethodName(string typeName)
    {
        return _typeNameToMethodName[typeName];
    }

    public void RegisterImplicitType(TypeModel model)
    {
        if (!_implicitTypeModels.ContainsKey(model.FullyQualifiedName))
        {
            _implicitTypeModels[model.FullyQualifiedName] = model;
        }
    }

    /// <summary>
    /// Registers a method name for a type without marking it for generation (assumes it exists elsewhere).
    /// </summary>
    public void RegisterExternalMethod(string typeFullName, string methodName)
    {
        _typeNameToMethodName[typeFullName] = methodName;
    }

    /// <summary>
    /// Gets or creates a helper method name for a type (implicit), tracking it for generation.
    /// </summary>
    public string GetOrCreateHelperMethodName(string typeFullName)
    {
        if (_typeNameToMethodName.TryGetValue(typeFullName, out string? existingMethod))
        {
            return existingMethod;
        }

        // Generate a unique helper method name based on the type
        string methodName = $"FastClonerSgClone{GetCleanTypeName(typeFullName)}";
        _typeNameToMethodName[typeFullName] = methodName;

        if (_neededHelperMethods.Add(typeFullName))
        {
            _pendingHelperMethods.Enqueue(typeFullName);
        }

        return methodName;
    }
    
    /// <summary>
    /// Gets or creates a helper method name for a member, tracking it for generation.
    /// </summary>
    public string GetOrCreateHelperMethodName(MemberModel member)
    {
        string typeKey = member.TypeFullName;

        if (_typeNameToMethodName.TryGetValue(typeKey, out string? existingMethod))
        {
            return existingMethod;
        }

        // Generate a unique helper method name based on the type
        string methodName = $"FastClonerSgClone{GetCleanTypeName(member.TypeFullName)}";
        _typeNameToMethodName[typeKey] = methodName;

        if (_neededHelperMethods.Add(typeKey))
        {
            _pendingHelperMethods.Enqueue(typeKey);
        }

        // Store the member model for later use when generating helper methods
        if (!_typeNameToMemberModel.ContainsKey(typeKey))
        {
            _typeNameToMemberModel[typeKey] = member;
        }

        return methodName;
    }

    /// <summary>
    /// Cleans a type name string for use in method names by replacing special characters.
    /// </summary>
    private static string GetCleanTypeName(string typeName)
    {
        return typeName
            .Replace("global::", "")
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace(',', '_')
            .Replace(' ', '_')
            .Replace('.', '_')
            .Replace('[', '_')
            .Replace(']', '_')
            .Replace('?', '_')
            .Replace(':', '_');
    }

    /// <summary>
    /// Registers a derived type helper for generation.
    /// </summary>
    public void RegisterDerivedTypeHelper(TypeModel derivedType, string methodName)
    {
        if (!_derivedTypeHelpers.ContainsKey(derivedType.FullyQualifiedName))
        {
            _derivedTypeHelpers[derivedType.FullyQualifiedName] = derivedType;
            _typeNameToMethodName[derivedType.FullyQualifiedName] = methodName;
        }
    }

    /// <summary>
    /// Gets all derived type helpers that need to be generated.
    /// </summary>
    public IEnumerable<(TypeModel Model, string MethodName)> GetDerivedTypeHelpers()
    {
        foreach (KeyValuePair<string, TypeModel> kvp in _derivedTypeHelpers)
        {
            yield return (kvp.Value, _typeNameToMethodName[kvp.Key]);
        }
    }

    /// <summary>
    /// Gets whether there are any derived type helpers to generate.
    /// </summary>
    public bool HasDerivedTypeHelpers => _derivedTypeHelpers.Count > 0;

    public void IncrementHelperUsage(string typeFullName)
    {
        if (_helperUsageCounts.TryGetValue(typeFullName, out int count))
        {
            _helperUsageCounts[typeFullName] = count + 1;
        }
        else
        {
            _helperUsageCounts[typeFullName] = 1;
        }
    }

    public int GetHelperUsageCount(string typeFullName)
    {
        return _helperUsageCounts.TryGetValue(typeFullName, out int count) ? count : 0;
    }

    public bool ShouldInline(string typeFullName)
    {
        // Only inline if used exactly once
        return GetHelperUsageCount(typeFullName) == 1;
    }

    private int _variableCounter = 0;
    public int GetNextVariableId() => System.Threading.Interlocked.Increment(ref _variableCounter);
    
    /// <summary>
    /// Gets the fully-qualified call to FastCloner.DeepClone for use in generated code.
    /// Uses global:: prefix to avoid namespace/class ambiguity (both are named "FastCloner").
    /// </summary>
    public static string FastClonerDeepCloneCall(string expression) => $"global::FastCloner.FastCloner.DeepClone({expression})";
    
    /// <summary>
    /// Gets the NotNullIfNotNull attribute string for generated methods, or empty if unavailable.
    /// </summary>
    public static string NotNullIfNotNullAttr(bool isAvailable, string paramName = "source") 
        => isAvailable 
            ? $"[return: global::System.Diagnostics.CodeAnalysis.NotNullIfNotNull(\"{paramName}\")]" 
            : "";
}
