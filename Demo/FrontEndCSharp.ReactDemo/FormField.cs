using System.Linq.Expressions;
using System.Reflection;

namespace FrontEndCSharp.ReactDemo;

public static class FormField<TModel>
{
    public static string IdFor<TValue>(Expression<Func<TModel, TValue>> selector) =>
        MemberNameFor(selector);

    internal static string MemberNameFor<TValue>(Expression<Func<TModel, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (selector.Body is not MemberExpression memberExpression
            || memberExpression.Expression is not ParameterExpression
            || memberExpression.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "The selector must access one direct readable instance property.",
                nameof(selector));
        }

        return property.Name;
    }
}
