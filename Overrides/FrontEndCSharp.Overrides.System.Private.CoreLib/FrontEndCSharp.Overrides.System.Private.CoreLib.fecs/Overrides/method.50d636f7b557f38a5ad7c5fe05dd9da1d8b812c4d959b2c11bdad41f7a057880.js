import { managedPrimitiveHash, runtimeIdentityHash } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Collections.Generic.IEqualityComparer`1[T].GetHashCode(T)'] = function (_ctx, _this, value) {
    let hash = managedPrimitiveHash(value);
    if (hash === null && value?.t === 'System.Type') {
        hash = managedPrimitiveHash(wrap(unwrap(value), 'System.String'));
    }
    return wrap(hash === null ? runtimeIdentityHash(value) : hash, 'System.Int32');
};
