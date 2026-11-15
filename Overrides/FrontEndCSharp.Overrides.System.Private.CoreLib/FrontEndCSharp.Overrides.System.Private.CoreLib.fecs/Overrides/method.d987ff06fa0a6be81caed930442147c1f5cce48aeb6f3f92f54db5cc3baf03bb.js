import { hashSetMoveNext } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1+Enumerator[T].MoveNext()'] = function (_ctx, enumerator) {
    return hashSetMoveNext(enumerator);
};
