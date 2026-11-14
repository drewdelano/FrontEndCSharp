import { managedPrimitiveHash, runtimeIdentityHash } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';
import { wrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.HashCode.Combine<T1, T2, T3, T4, T5>(T1, T2, T3, T4, T5)'] = function (_ctx, ...values) {
    let hash = 17;
    for (const value of values) {
        hash = (Math.imul(hash, 31) + (managedPrimitiveHash(value) ?? runtimeIdentityHash(value))) | 0;
    }
    return wrap(hash, 'System.Int32');
};
