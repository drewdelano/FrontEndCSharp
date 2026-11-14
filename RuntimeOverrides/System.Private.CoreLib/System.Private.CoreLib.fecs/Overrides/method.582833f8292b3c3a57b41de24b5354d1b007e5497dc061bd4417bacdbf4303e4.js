import { dictionaryRemove } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].Remove(TKey)'] = function (ctx, $this, key) {
    return dictionaryRemove(ctx, $this, key);
};
