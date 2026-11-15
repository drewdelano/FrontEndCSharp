import { dictionaryRemove } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].Remove(System.Object)'] = function (ctx, $this, key) {
    dictionaryRemove(ctx, $this, key);
};
