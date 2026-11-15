import { listIndexOf } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].IndexOf(T)'] = function (ctx, $this, value) {
    return listIndexOf(ctx, $this, value);
};
