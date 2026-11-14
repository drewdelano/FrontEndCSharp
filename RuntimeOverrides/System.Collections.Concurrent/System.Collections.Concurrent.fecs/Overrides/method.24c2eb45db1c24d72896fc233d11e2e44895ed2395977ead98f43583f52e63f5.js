import { initializeConcurrentDictionary } from '../runtime-assets/System.Collections.Concurrent.fecs/managed-concurrent-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Concurrent.ConcurrentDictionary`2[TKey, TValue]..ctor()'] = function (_ctx, dictionary) {
    initializeConcurrentDictionary(dictionary);
};
