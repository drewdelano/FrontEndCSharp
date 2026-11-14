import { dictionaryContainsKey } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].ContainsKey(TKey)'] = function (ctx, $this, key) {
    return dictionaryContainsKey(ctx, $this, key);
};
