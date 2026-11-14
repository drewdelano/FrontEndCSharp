import { initializeHashSet } from '../runtime-assets/System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1[T]..ctor()'] = function (_ctx, set) {
    initializeHashSet(set);
};
