import { stackEnumeratorDispose } from '../runtime-assets/FrontEndCSharp.Overrides.System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1+Enumerator[T].Dispose()'] = function () {
    stackEnumeratorDispose();
};
