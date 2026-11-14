import { listSet } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].set_Item(System.Int32, System.Object)'] = function (ctx, $this, index, value) {
    listSet(ctx, $this, index, value);
};
