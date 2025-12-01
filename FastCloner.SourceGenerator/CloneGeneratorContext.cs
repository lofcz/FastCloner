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
    
    private readonly Dictionary<string, string> _typeNameToMethodName = new();
    private readonly HashSet<string> _neededHelperMethods = new();
    private readonly Queue<string> _pendingHelperMethods = new();
    private readonly Dictionary<string, MemberModel> _typeNameToMemberModel = new();
    private readonly Dictionary<string, TypeModel> _implicitTypeModels = new();

    public bool NeedsStateClass { get; set; }
    public bool NeedsClonerClass { get; set; }
    
    public bool CanHaveCircularReferences { get; }
    public bool IsFastClonerAvailable { get; }

    public CloneGeneratorContext(TypeModel model)
    {
        Model = model;
        CanHaveCircularReferences = model.CanHaveCircularReferences;
        IsFastClonerAvailable = model.IsFastClonerAvailable;

        foreach (var related in model.RelatedTypes)
        {
            _implicitTypeModels[related.FullyQualifiedName] = related;
        }
        
        foreach (var nested in model.NestedTypes)
        {
            if (!_typeNameToMemberModel.ContainsKey(nested.TypeFullName))
            {
                _typeNameToMemberModel[nested.TypeFullName] = nested;
            }
        }
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

    /// <summary>
    /// Gets or creates a helper method name for a type (implicit), tracking it for generation.
    /// </summary>
    public string GetOrCreateHelperMethodName(string typeFullName)
    {
        if (_typeNameToMethodName.TryGetValue(typeFullName, out var existingMethod))
        {
            return existingMethod;
        }

        // Generate a unique helper method name based on the type
        var methodName = $"FastClonerSgClone{GetCleanTypeName(typeFullName)}";
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
        var typeKey = member.TypeFullName;

        if (_typeNameToMethodName.TryGetValue(typeKey, out var existingMethod))
        {
            return existingMethod;
        }

        // Generate a unique helper method name based on the type
        var methodName = $"FastClonerSgClone{GetCleanTypeName(member.TypeFullName)}";
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
}
