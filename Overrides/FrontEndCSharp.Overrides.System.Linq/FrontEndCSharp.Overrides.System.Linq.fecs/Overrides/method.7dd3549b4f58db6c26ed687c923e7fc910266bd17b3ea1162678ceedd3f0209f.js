import { invokeManagedDelegate } from '../jit-engine/calls.js';
import { getManagedTypeName, wrap } from '../msil-type-helpers.js';
import { materializeManagedEnumerable } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-enumeration.js';

globalThis.DotNet.Methods['System.Linq.Enumerable.Select<TSource, TResult>(System.Collections.Generic.IEnumerable`1[T], System.Func`2[T, TResult])'] = function (ctx, source, selector) {
    const resultType = ctx.genericParams?.TResult;
    const results = materializeManagedEnumerable(ctx, source)
        .map(value => invokeManagedDelegate(ctx, selector, [value]));
    return wrap(results, `${getManagedTypeName(resultType)}[]`);
};
