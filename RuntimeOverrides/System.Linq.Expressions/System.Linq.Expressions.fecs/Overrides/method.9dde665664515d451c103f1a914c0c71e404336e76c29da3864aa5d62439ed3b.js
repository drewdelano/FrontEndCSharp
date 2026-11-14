import { createProperty } from '../runtime-assets/System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.Expression.Property(System.Linq.Expressions.Expression, System.Reflection.MethodInfo)'] = function (ctx, expression, accessor) {
    return createProperty(expression, accessor);
};
