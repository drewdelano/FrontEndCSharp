import { stackEnumeratorCurrent } from '../runtime-assets/System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1+Enumerator[T].get_Current()'] = function (ctx, enumerator) {
    return stackEnumeratorCurrent(ctx, enumerator);
};
