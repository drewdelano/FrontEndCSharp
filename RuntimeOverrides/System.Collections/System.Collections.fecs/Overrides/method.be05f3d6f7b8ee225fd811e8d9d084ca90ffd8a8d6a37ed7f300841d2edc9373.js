import { stackCount } from '../runtime-assets/System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1[T].get_Count()'] = function (ctx, stack) {
    return stackCount(ctx, stack);
};
