import { stackEnumeratorMoveNext } from '../runtime-assets/System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1+Enumerator[T].MoveNext()'] = function (ctx, enumerator) {
    return stackEnumeratorMoveNext(ctx, enumerator);
};
