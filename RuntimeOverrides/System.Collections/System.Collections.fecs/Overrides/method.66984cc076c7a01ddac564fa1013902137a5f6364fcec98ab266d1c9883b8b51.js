import { stackGetEnumerator } from '../runtime-assets/System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1[T].GetEnumerator()'] = function (ctx, stack) {
    return stackGetEnumerator(ctx, stack);
};
