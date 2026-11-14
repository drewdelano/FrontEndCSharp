import { dictionaryClear } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue].Clear()'] = function (ctx, $this) {
    dictionaryClear(ctx, $this);
};
