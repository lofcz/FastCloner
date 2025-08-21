using System.Drawing;
using FastCloner.Code;

namespace FastCloner.Contrib;

public static class ContribTypeHandlers
{
    private static bool registered;
    
    public static void Register()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        
        FastClonerExprGenerator.CustomTypeHandlers.TryAdd(
            typeof(Font), 
            ProcessFont
        );
    }

    private static object ProcessFont(Type type, bool unboxStruct, FastClonerExprGenerator.ExpressionPosition position)
    {
        return (Func<object, FastCloneState, object>)(static (obj, state) =>
        {
            Font font = (Font)obj;
            Font result = (Font)font.Clone();
            state.AddKnownRef(obj, result);
            return result;
        });
    }
}