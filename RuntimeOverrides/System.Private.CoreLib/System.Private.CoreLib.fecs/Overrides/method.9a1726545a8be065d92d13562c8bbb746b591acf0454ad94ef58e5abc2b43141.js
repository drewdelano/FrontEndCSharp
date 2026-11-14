import { getLazyValue } from '../runtime-assets/System.Private.CoreLib.fecs/managed-lazy.js';
globalThis.DotNet.Methods['System.Lazy`1[T].get_Value()'] = function (ctx, lazy) {
    return getLazyValue(ctx, lazy);
};
