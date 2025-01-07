using System.Reflection;

namespace FastCloner.Helpers;

internal static class ReflectionHelper
{
    public static bool IsEnum(this Type t) => t.IsEnum;

    public static bool IsValueType(this Type t) => t.IsValueType;

    public static bool IsClass(this Type t) => t.IsClass;

    public static Type? BaseType(this Type t) => t.BaseType;

    public static FieldInfo[] GetAllFields(this Type t) => t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public static PropertyInfo[] GetPublicProperties(this Type t) => t.GetProperties(BindingFlags.Instance | BindingFlags.Public);

    public static FieldInfo[] GetDeclaredFields(this Type t) => t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);

    public static ConstructorInfo[] GetPrivateConstructors(this Type t) => t.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);

    public static ConstructorInfo[] GetPublicConstructors(this Type t) => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

    public static MethodInfo? GetPrivateMethod(this Type t, string methodName) => t.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

    public static MethodInfo? GetMethod(this Type t, string methodName) => t.GetMethod(methodName);

    public static MethodInfo? GetPrivateStaticMethod(this Type t, string methodName) => t.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);

    public static FieldInfo? GetPrivateField(this Type t, string fieldName) => t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

    public static FieldInfo? GetPrivateStaticField(this Type t, string fieldName) => t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);

    public static bool IsSubclassOfTypeByName(this Type t, string typeName)
    {
        while (t != null)
        {
            if (t.Name == typeName)
                return true;
            t = t.BaseType();
        }

        return false;
    }

    public static Type[] GenericArguments(this Type t) => t.GetGenericArguments();
}