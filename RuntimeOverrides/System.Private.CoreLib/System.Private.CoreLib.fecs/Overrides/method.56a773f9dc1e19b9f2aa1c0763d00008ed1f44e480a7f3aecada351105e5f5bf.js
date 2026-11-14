import { listAdd } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';
import { materializeManagedEnumerable } from '../runtime-assets/System.Private.CoreLib.fecs/task-enumeration.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T].AddRange(System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, list, source) {
    for (const value of materializeManagedEnumerable(ctx, source)) listAdd(ctx, list, value);
};
