import { concurrentDictionaryGetOrAdd } from '../runtime-assets/System.Collections.Concurrent.fecs/managed-concurrent-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Concurrent.ConcurrentDictionary`2[TKey, TValue].GetOrAdd<TArg>(TKey, System.Func`3[T1, T2, TResult], TArg)'] = function (ctx, dictionary, key, valueFactory, factoryArgument) {
    return concurrentDictionaryGetOrAdd(ctx, dictionary, key, valueFactory, factoryArgument);
};
