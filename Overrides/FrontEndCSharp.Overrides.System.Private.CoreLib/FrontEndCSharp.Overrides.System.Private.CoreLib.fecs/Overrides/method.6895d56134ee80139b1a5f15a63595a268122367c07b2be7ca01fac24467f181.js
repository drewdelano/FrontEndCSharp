import { initializeDictionary } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue]..ctor(System.Int32, System.Collections.Generic.IEqualityComparer`1[T])'] = function (ctx, $this, capacity, comparer) {
    initializeDictionary(ctx, $this, capacity, comparer);
};
