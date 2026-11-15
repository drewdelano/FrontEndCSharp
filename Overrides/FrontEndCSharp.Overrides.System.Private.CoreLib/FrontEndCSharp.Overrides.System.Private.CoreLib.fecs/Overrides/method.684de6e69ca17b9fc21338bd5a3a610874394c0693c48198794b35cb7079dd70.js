import { listCount } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].get_Count()'] = function (ctx, $this) {
    return listCount(ctx, $this);
};
