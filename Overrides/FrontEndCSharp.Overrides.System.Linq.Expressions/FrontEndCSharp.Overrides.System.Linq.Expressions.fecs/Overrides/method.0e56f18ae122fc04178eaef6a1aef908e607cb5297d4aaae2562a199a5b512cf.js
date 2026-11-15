import { getMemberExpression } from '../runtime-assets/FrontEndCSharp.Overrides.System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.MemberExpression.get_Expression()'] = function (ctx, expression) {
    return getMemberExpression(expression);
};
