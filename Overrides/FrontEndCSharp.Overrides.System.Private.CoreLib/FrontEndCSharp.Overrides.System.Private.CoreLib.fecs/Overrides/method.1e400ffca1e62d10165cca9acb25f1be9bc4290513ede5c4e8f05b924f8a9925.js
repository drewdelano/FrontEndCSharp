import { dictionarySet } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].set_Item(TKey, TValue)'] = function (ctx, $this, key, value) {
    dictionarySet(ctx, $this, key, value);
};
