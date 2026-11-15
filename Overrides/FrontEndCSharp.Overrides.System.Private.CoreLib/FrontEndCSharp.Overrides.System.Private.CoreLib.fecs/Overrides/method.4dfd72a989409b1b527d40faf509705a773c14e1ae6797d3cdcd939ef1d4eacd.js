import { listAdd } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].Add(T)'] = function (ctx, $this, value) {
    listAdd(ctx, $this, value);
};
