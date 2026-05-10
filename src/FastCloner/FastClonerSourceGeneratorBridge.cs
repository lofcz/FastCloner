using System.Reflection;
using FastCloner.Code;

namespace FastCloner;

// used implicitly
[FastClonerSourceGeneratorBridge("FastCloner.SourceGenerated.__FastClonerSGBridgeProxy")]
internal static class FastClonerSourceGeneratorBridge
{
    [FastClonerSourceGeneratorBridgeMember]
    internal static void DeepCloneField(object source, object target, FieldInfo field)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (field is null) throw new ArgumentNullException(nameof(field));

        object? value = field.GetValue(source);
        object? cloned = FastClonerGenerator.CloneObject(value);
        FieldAccessorGenerator.GetFieldSetter(field)(target, cloned!);
    }
    
    [FastClonerSourceGeneratorBridgeMember]
    internal static void CopyField(object source, object target, FieldInfo field)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (field is null) throw new ArgumentNullException(nameof(field));

        object? value = field.GetValue(source);
        FieldAccessorGenerator.GetFieldSetter(field)(target, value!);
    }
    
    [FastClonerSourceGeneratorBridgeMember]
    internal static FieldInfo ResolveDeclaredField(Type declaringType, string fieldName)
    {
        if (declaringType is null) throw new ArgumentNullException(nameof(declaringType));
        if (fieldName is null) throw new ArgumentNullException(nameof(fieldName));

        FieldInfo? field = declaringType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        if (field == null)
        {
            throw new InvalidOperationException(
                $"FastCloner source-generator bridge: field '{fieldName}' was not found on '{declaringType.FullName}'. " +
                "The type shape may have changed since the source generator ran.");
        }

        return field;
    }
    
    [FastClonerSourceGeneratorBridgeMember]
    internal static void DeepCloneProperty(object source, object target, PropertyInfo property)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (property is null) throw new ArgumentNullException(nameof(property));

        MethodInfo? setter = property.GetSetMethod(nonPublic: true);
        if (setter == null)
        {
            throw new InvalidOperationException(
                $"FastCloner source-generator bridge: property '{property.Name}' on '{property.DeclaringType?.FullName}' has no setter.");
        }

        object? value = property.GetValue(source);
        object? cloned = FastClonerGenerator.CloneObject(value);
        setter.Invoke(target, [cloned]);
    }
    
    [FastClonerSourceGeneratorBridgeMember]
    internal static PropertyInfo ResolveDeclaredProperty(Type declaringType, string propertyName)
    {
        if (declaringType is null) throw new ArgumentNullException(nameof(declaringType));
        if (propertyName is null) throw new ArgumentNullException(nameof(propertyName));

        PropertyInfo? property = declaringType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        if (property == null)
        {
            throw new InvalidOperationException(
                $"FastCloner source-generator bridge: property '{propertyName}' was not found on '{declaringType.FullName}'.");
        }

        return property;
    }
}
