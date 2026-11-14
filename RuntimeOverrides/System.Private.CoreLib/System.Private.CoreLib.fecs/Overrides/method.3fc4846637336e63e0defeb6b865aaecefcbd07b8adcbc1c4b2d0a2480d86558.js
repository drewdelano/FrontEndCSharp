import { listIndexOf } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].IndexOf(System.Object)'] = function (ctx, $this, value) {
    return listIndexOf(ctx, $this, value);
};
