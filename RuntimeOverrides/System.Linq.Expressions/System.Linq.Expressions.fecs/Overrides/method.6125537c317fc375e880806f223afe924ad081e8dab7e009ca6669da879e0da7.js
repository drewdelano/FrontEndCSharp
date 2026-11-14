import { compileLambda } from '../runtime-assets/System.Linq.Expressions.fecs/expression-trees.js';

globalThis.DotNet.Methods['System.Linq.Expressions.Expression`1[TDelegate].Compile()'] = function (ctx, expression) {
    return compileLambda(
        ctx,
        expression,
        ctx.genericParams?.TDelegate ?? expression.$typeArgs?.TDelegate);
};
