import { createLambda } from '../runtime-assets/FrontEndCSharp.Overrides.System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.Expression.Lambda<TDelegate>(System.Linq.Expressions.Expression, System.Linq.Expressions.ParameterExpression[])'] = function (ctx, body, parameters) {
    return createLambda(body, parameters, ctx.genericParams?.TDelegate);
};
