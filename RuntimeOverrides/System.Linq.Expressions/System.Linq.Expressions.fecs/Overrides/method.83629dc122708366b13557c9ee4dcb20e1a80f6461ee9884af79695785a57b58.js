import { getMember } from '../runtime-assets/System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.MemberExpression.get_Member()'] = function (ctx, expression) {
    return getMember(expression);
};
