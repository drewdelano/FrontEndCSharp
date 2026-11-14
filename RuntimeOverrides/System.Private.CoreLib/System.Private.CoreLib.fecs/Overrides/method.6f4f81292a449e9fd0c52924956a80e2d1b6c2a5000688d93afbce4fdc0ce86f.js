import { initializeDictionary } from '../runtime-assets/System.Private.CoreLib.fecs/managed-dictionary.js';

globalThis.DotNet.Methods['System.Collections.Generic.Dictionary`2[TKey, TValue]..ctor(System.Int32)'] = function (ctx, $this, capacity) {
    initializeDictionary(ctx, $this, capacity);
};
