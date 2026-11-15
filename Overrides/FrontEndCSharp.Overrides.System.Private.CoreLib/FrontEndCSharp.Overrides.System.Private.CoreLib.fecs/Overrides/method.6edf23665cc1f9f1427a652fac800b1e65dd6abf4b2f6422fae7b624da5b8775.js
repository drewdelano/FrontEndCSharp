import { listAdd } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Add(System.Object)'] = function (ctx, $this, value) {
    return listAdd(ctx, $this, value);
};
