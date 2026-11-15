import { initializeHashSet } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1[T]..ctor(System.Collections.Generic.IEqualityComparer`1[T])'] = function (_ctx, set, comparer) {
    initializeHashSet(set, comparer);
};
