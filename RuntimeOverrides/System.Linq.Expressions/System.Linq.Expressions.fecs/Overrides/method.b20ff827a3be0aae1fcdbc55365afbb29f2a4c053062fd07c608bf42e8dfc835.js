import { getExpressionNodeType } from '../runtime-assets/System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.Expression.get_NodeType()'] = function (_ctx, expression) {
    return getExpressionNodeType(expression);
};
