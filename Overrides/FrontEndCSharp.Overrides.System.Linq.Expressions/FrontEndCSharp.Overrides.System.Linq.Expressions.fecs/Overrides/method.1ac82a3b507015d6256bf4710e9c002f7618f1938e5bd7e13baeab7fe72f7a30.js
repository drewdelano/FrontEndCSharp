import { getLambdaBody } from '../runtime-assets/FrontEndCSharp.Overrides.System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.LambdaExpression.get_Body()'] = function (ctx, expression) {
    return getLambdaBody(expression);
};
