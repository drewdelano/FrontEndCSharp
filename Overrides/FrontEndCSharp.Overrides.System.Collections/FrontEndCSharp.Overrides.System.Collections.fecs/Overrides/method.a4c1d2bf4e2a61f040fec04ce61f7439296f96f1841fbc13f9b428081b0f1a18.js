import { stackPop } from '../runtime-assets/FrontEndCSharp.Overrides.System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1[T].Pop()'] = function (ctx, stack) {
    return stackPop(ctx, stack);
};
