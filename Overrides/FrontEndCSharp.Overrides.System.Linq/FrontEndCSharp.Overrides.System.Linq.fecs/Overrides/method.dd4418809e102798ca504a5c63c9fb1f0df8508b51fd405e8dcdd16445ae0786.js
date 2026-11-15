import { getManagedTypeName, wrap } from '../msil-type-helpers.js';
import { materializeManagedEnumerable } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-enumeration.js';

globalThis.DotNet.Methods['System.Linq.Enumerable.ToArray<TSource>(System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, source) {
    const elementType = ctx.genericParams?.TSource;
    return wrap(materializeManagedEnumerable(ctx, source), `${getManagedTypeName(elementType)}[]`);
};
