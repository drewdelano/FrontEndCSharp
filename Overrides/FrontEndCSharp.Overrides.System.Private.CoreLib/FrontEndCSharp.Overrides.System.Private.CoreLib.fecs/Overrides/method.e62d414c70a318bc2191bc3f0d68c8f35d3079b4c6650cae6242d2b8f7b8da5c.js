import { keyValuePairValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.KeyValuePair`2[TKey, TValue].get_Value()'] = function (ctx, $this) {
    return keyValuePairValue(ctx, $this);
};
