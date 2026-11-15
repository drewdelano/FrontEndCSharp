import { expressionToString } from '../runtime-assets/FrontEndCSharp.Overrides.System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.Expression.ToString()'] = function (_ctx, expression) {
    return expressionToString(expression);
};
