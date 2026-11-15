import { invokeManagedDelegate } from '../jit-engine/calls.js';
import { listForEach } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].ForEach(System.Action`1[T])'] = function (ctx, list, action) {
    listForEach(ctx, list, action, invokeManagedDelegate);
};
