import { listEnumeratorCurrent } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1+Enumerator[T].get_Current()'] = function (ctx, $this) {
    return listEnumeratorCurrent(ctx, $this);
};
