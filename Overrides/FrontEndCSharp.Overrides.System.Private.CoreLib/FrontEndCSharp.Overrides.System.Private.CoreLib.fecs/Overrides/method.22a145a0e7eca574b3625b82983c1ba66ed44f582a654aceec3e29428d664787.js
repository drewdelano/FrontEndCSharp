import { listToArray } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].ToArray()'] = function (ctx, list) {
    return listToArray(ctx, list);
};
