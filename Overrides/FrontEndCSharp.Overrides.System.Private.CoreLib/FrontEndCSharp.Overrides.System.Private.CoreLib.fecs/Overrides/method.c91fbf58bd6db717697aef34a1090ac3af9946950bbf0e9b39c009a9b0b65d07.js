import { listGet } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].get_Item(System.Int32)'] = function (ctx, $this, index) {
    return listGet(ctx, $this, index);
};
