import { dictionaryTryGetValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].TryGetValue(TKey, TValue&)'] = function (ctx, dictionary, key, value) {
    return dictionaryTryGetValue(ctx, dictionary, key, value);
};
