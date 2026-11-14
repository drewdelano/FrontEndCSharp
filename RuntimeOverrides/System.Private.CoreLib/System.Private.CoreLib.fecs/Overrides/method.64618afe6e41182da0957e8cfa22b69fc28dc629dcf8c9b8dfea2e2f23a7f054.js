import { keyValuePairKey } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.KeyValuePair`2[TKey, TValue].get_Key()'] = function (ctx, $this) {
    return keyValuePairKey(ctx, $this);
};
