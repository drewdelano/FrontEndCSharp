import { getManagedTypeName, unwrap, wrap } from '../msil-type-helpers.js';

function invokeVirtual(ctx, instance, methodName) {
    let typeName = instance?.t;
    while (typeof typeName === 'string') {
        const method = globalThis.DotNet.Methods[`${typeName}.${methodName}`];
        if (typeof method === 'function') {
            const previousGenericParams = ctx.genericParams;
            try {
                ctx.genericParams = instance.$typeArgs ?? null;
                return method(ctx, instance);
            } finally {
                ctx.genericParams = previousGenericParams;
            }
        }
        const baseType = globalThis.DotNet.Types[typeName]?.baseType;
        typeName = baseType == null ? null : getManagedTypeName(baseType);
    }
    throw new Error(`Managed method '${instance?.t}.${methodName}' was not emitted.`);
}

globalThis.DotNet.Methods['System.String.Join(System.String, System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, separator, values) {
    const delimiter = unwrap(separator) ?? '';
    const enumerator = invokeVirtual(ctx, values, 'GetEnumerator()');
    const parts = [];
    try {
        while (unwrap(invokeVirtual(ctx, enumerator, 'MoveNext()'))) {
            const current = invokeVirtual(ctx, enumerator, 'get_Current()');
            const value = unwrap(current);
            parts.push(value == null ? '' : String(value));
        }
    } finally {
        invokeVirtual(ctx, enumerator, 'Dispose()');
    }
    return wrap(parts.join(delimiter), 'System.String');
};
