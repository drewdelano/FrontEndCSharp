import { stackEnumeratorMoveNext } from '../runtime-assets/FrontEndCSharp.Overrides.System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1+Enumerator[T].MoveNext()'] = function (ctx, enumerator) {
    return stackEnumeratorMoveNext(ctx, enumerator);
};
