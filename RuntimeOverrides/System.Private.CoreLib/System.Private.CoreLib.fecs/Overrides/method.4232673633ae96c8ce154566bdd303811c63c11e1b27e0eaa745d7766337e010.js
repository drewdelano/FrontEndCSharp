import { hashSetGetEnumerator } from '../runtime-assets/System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1[T].GetEnumerator()'] = function (_ctx, set) {
    return hashSetGetEnumerator(set);
};
