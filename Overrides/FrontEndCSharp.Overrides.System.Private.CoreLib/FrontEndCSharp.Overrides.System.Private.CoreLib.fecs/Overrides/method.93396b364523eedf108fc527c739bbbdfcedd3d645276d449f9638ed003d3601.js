import { dictionaryEnumeratorValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2+Enumerator[TKey, TValue].get_Value()'] = function (ctx, $this) {
    return dictionaryEnumeratorValue(ctx, $this);
};
