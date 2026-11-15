import { listRemove } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Remove(T)'] = function (ctx, $this, value) {
    return listRemove(ctx, $this, value);
};
