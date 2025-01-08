namespace FastCloner.Code;

internal interface IFastClonerTypeRegistry
{
    void RegisterTypeHandler(Type type, object handler);
}