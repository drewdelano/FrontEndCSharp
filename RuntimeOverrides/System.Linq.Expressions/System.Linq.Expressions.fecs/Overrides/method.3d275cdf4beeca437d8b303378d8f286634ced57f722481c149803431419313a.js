import { getParameterName } from '../runtime-assets/System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.ParameterExpression.get_Name()'] = function (ctx, expression) {
    return getParameterName(expression);
};
