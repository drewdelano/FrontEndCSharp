import { dictionaryEnumeratorMoveNext } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2+Enumerator[TKey, TValue].MoveNext()'] = function (ctx, $this) {
    return dictionaryEnumeratorMoveNext(ctx, $this);
};
