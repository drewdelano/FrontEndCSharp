import { getLambdaReturnType } from '../runtime-assets/System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.LambdaExpression.get_ReturnType()'] = function (ctx, expression) {
    return getLambdaReturnType(expression);
};
