import { initializeList } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T]..ctor(System.Int32)'] = function (ctx, $this, capacity) {
    initializeList(ctx, $this, capacity);
};
