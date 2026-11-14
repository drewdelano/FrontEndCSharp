import { initializeDictionaryFromDictionary } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';
globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue]..ctor(System.Collections.Generic.IDictionary`2[TKey, TValue], System.Collections.Generic.IEqualityComparer`1[T])'] = function (ctx, dictionary, source, comparer) {
    initializeDictionaryFromDictionary(ctx, dictionary, source, comparer);
};
