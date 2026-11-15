import { listContains } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Contains(System.Object)'] = function (ctx, $this, value) {
    return listContains(ctx, $this, value);
};
