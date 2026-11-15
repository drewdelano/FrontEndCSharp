import { listInsert } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Insert(System.Int32, T)'] = function (ctx, $this, index, value) {
    listInsert(ctx, $this, index, value);
};
