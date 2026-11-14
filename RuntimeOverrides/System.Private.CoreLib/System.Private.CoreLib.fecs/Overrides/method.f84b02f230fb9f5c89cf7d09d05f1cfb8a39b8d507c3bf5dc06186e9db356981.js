import { dictionaryEnumeratorCurrent } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2+Enumerator[TKey, TValue].get_Current()'] = function (ctx, $this) {
    return dictionaryEnumeratorCurrent(ctx, $this);
};
