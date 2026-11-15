import { dictionaryGet } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].get_Item(System.Object)'] = function (ctx, $this, key) {
    return dictionaryGet(ctx, $this, key);
};
