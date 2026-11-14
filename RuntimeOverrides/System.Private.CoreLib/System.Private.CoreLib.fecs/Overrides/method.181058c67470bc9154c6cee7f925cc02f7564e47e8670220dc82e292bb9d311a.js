import { dictionaryEnumeratorReset } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2+Enumerator[TKey, TValue].Reset()'] = function (ctx, $this) {
    dictionaryEnumeratorReset(ctx, $this);
};
