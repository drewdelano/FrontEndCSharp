import { listRemoveAt } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].RemoveAt(System.Int32)'] = function (ctx, $this, index) {
    listRemoveAt(ctx, $this, index);
};
