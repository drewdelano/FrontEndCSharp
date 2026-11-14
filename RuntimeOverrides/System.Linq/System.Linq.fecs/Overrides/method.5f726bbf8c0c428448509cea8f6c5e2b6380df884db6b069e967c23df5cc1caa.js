import { initializeList, listAdd } from '../runtime-assets/System.Private.CoreLib.fecs/managed-list.js';
import { wrap } from '../msil-type-helpers.js';
import { materializeManagedEnumerable } from '../runtime-assets/System.Private.CoreLib.fecs/task-enumeration.js';

globalThis.DotNet.Methods['System.Linq.Enumerable.ToList<TSource>(System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, source) {
    const elementType = ctx.genericParams?.TSource;
    const list = wrap({}, 'System.Collections.Generic.List`1[T]', { T: elementType });
    initializeList(ctx, list);
    for (const value of materializeManagedEnumerable(ctx, source)) listAdd(ctx, list, value);
    return list;
};
