import { createParameter } from '../runtime-assets/FrontEndCSharp.Overrides.System.Linq.Expressions.fecs/expression-trees.js';
globalThis.DotNet.Methods['System.Linq.Expressions.Expression.Parameter(System.Type, System.String)'] = function (ctx, type, name) {
    return createParameter(type, name);
};
