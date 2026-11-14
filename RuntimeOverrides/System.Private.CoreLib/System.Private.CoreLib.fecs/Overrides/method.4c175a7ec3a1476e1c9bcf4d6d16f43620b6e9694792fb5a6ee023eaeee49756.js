import { initializeDictionaryFromDictionary } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';
globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue]..ctor(System.Collections.Generic.IDictionary`2[TKey, TValue])'] = function (ctx, dictionary, source) {
    initializeDictionaryFromDictionary(ctx, dictionary, source);
};
