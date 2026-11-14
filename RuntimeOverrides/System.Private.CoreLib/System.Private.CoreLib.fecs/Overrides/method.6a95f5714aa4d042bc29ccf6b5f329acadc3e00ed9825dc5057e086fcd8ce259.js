import { listContains } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Contains(T)'] = function (ctx, $this, value) {
    return listContains(ctx, $this, value);
};
