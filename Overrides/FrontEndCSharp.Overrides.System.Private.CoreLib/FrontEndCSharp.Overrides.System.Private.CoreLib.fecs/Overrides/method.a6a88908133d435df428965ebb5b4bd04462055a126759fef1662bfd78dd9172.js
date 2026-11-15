import { dictionaryCount } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].get_Count()'] = function (ctx, $this) {
    return dictionaryCount(ctx, $this);
};
