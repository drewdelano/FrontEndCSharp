import { initializeHashSetFromEnumerable } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1[T]..ctor(System.Collections.Generic.IEnumerable`1[T], System.Collections.Generic.IEqualityComparer`1[T])'] = function (ctx, set, source, comparer) {
    initializeHashSetFromEnumerable(ctx, set, source, comparer);
};
