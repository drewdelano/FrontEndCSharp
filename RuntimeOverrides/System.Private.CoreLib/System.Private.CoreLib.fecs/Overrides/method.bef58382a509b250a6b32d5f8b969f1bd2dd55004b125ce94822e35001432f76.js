import { dictionaryGetEnumerator } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].GetEnumerator()'] = function (ctx, $this) {
    return dictionaryGetEnumerator(ctx, $this);
};
