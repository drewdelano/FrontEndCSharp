import { listClear } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Clear()'] = function (ctx, $this) {
    listClear(ctx, $this);
};
