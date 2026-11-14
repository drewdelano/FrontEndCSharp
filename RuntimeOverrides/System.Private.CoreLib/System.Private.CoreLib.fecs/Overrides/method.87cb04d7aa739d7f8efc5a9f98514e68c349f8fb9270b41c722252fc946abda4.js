import { listGetEnumerator } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].GetEnumerator()'] = function (ctx, $this) {
    return listGetEnumerator(ctx, $this);
};
