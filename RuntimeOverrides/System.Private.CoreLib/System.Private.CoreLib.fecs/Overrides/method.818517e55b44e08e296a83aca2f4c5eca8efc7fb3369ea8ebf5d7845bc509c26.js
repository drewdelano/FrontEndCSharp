import { initializeDictionary } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue]..ctor()'] = function (ctx, $this) {
    initializeDictionary(ctx, $this);
};
