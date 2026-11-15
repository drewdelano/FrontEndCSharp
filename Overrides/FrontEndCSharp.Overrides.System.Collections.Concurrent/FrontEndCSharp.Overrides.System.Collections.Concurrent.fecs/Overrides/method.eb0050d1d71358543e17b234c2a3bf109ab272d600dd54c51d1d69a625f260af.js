import { concurrentDictionaryGetOrAdd } from '../runtime-assets/FrontEndCSharp.Overrides.System.Collections.Concurrent.fecs/managed-concurrent-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Concurrent.ConcurrentDictionary`2[TKey, TValue].GetOrAdd(TKey, System.Func`2[T, TResult])'] = function (ctx, dictionary, key, valueFactory) {
    return concurrentDictionaryGetOrAdd(ctx, dictionary, key, valueFactory);
};
