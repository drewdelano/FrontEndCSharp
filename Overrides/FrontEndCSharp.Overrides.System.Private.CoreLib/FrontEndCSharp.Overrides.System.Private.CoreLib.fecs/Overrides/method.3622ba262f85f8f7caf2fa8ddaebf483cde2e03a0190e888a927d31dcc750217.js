import { listRemove } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Remove(System.Object)'] = function (ctx, $this, value) {
    listRemove(ctx, $this, value);
};
