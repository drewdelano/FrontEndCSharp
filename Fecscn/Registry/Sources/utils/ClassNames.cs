
namespace Fecscn.Components.UI;

public static class ClassNames
{
    public static string Merge(params string?[] values)
    {
        var count = 0;
        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                count++;
            }
        }
        var inputs = new string[count];
        var target = 0;
        for (var index = 0; index < values.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(values[index]))
            {
                inputs[target++] = values[index]!;
            }
        }
        return Interop.JS.Shadcn.Cn.ClassNames.Merge(inputs);
    }
}
