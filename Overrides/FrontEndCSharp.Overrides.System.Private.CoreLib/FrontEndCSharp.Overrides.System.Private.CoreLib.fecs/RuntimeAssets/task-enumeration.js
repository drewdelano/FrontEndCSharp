import { throwManagedException } from '../../managed-exceptions.js';
import { getManagedTypeName, unwrap } from '../../msil-type-helpers.js';

export function materializeManagedEnumerable(ctx, enumerable) {
    const rawEnumerable = unwrap(enumerable);
    if (Array.isArray(rawEnumerable)) {
        return rawEnumerable;
    }

    const enumerator = invokeVirtual(ctx, enumerable, 'GetEnumerator()');
    const tasks = [];
    try {
        while (unwrap(invokeVirtual(ctx, enumerator, 'MoveNext()'))) {
            tasks.push(invokeVirtual(ctx, enumerator, 'get_Current()'));
        }
    } finally {
        invokeVirtual(ctx, enumerator, 'Dispose()');
    }
    return tasks;
}

export const materializeTaskEnumerable = materializeManagedEnumerable;

function invokeVirtual(ctx, instance, methodName) {
    let typeName = instance?.t;
    while (typeof typeName === 'string') {
        const publicName = `${typeName}.${methodName}`;
        let method = globalThis.DotNet.Methods[publicName];
        if (typeof method !== 'function') {
            const prefix = `${typeName}.`;
            const suffix = `.${methodName}`;
            const explicitCandidates = Object.keys(globalThis.DotNet.Methods)
                .filter(name => name.startsWith(prefix) && name.endsWith(suffix))
                .sort((left, right) => {
                    const leftGeneric = left.includes('.System.Collections.Generic.');
                    const rightGeneric = right.includes('.System.Collections.Generic.');
                    return leftGeneric === rightGeneric
                        ? left.localeCompare(right)
                        : leftGeneric ? -1 : 1;
                });
            method = explicitCandidates.length === 0
                ? undefined
                : globalThis.DotNet.Methods[explicitCandidates[0]];
        }
        if (typeof method === 'function') {
            const previousGenericParams = ctx.genericParams;
            try {
                ctx.genericParams = instance?.$typeArgs ?? previousGenericParams;
                return method(ctx, instance);
            } finally {
                ctx.genericParams = previousGenericParams;
            }
        }
        const baseType = globalThis.DotNet.Types[typeName]?.baseType;
        typeName = baseType == null ? null : getManagedTypeName(baseType);
    }
    throwManagedException(
        ctx,
        'System.MissingMethodException',
        `Managed method '${instance?.t ?? 'unknown'}.${methodName}' was not emitted.`);
}
