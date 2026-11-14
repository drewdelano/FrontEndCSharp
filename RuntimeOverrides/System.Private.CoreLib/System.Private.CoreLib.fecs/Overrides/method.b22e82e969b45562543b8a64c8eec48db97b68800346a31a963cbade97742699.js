import { hashSetCurrent } from '../runtime-assets/System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1+Enumerator[T].get_Current()'] = function (_ctx, enumerator) {
    return hashSetCurrent(enumerator);
};
