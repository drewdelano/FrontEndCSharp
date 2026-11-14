import { isManagedNull, wrap } from '../msil-type-helpers.js';

function sameInvocation(left, right) {
    return left.target === right.target
        && left.functionPointer.methodRef.methodName === right.functionPointer.methodRef.methodName
        && left.functionPointer.methodRef.typeName === right.functionPointer.methodRef.typeName;
}

globalThis.DotNet.Methods['System.Delegate.Remove(System.Delegate, System.Delegate)'] = function (_ctx, source, value) {
    if (isManagedNull(source) || isManagedNull(value)) return source;
    const sourceInvocations = source.v.invocations;
    const removedInvocations = value.v.invocations;
    for (let start = sourceInvocations.length - removedInvocations.length; start >= 0; start--) {
        if (!removedInvocations.every((entry, index) => sameInvocation(sourceInvocations[start + index], entry))) continue;
        const remaining = [
            ...sourceInvocations.slice(0, start),
            ...sourceInvocations.slice(start + removedInvocations.length)
        ];
        return remaining.length === 0
            ? null
            : wrap({ invocations: remaining }, source.t, source.$typeArgs ?? null);
    }
    return source;
};
