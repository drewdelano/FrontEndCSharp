import { dictionaryAdd } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].Add(System.Object, System.Object)'] = function (ctx, $this, key, value) {
    dictionaryAdd(ctx, $this, key, value);
};
