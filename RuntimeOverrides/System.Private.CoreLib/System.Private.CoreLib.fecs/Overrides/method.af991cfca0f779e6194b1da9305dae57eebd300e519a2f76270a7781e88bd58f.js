import { hashSetRemove } from '../runtime-assets/System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1[T].Remove(T)'] = function (_ctx, set, value) {
    return hashSetRemove(set, value);
};
