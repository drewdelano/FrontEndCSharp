import { dictionaryEnumeratorKey } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2+Enumerator[TKey, TValue].get_Key()'] = function (ctx, $this) {
    return dictionaryEnumeratorKey(ctx, $this);
};
