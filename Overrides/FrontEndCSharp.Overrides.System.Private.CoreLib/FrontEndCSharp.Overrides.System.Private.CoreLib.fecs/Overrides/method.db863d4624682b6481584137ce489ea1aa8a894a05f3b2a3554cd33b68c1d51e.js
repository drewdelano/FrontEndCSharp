import { dictionaryContainsKey } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].Contains(System.Object)'] = function (ctx, $this, key) {
    return dictionaryContainsKey(ctx, $this, key);
};
