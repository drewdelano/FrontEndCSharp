import { managedPrimitiveEquals } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Collections.Generic.EqualityComparer`1[T].Equals(T, T)'] = function (_ctx, _this, left, right) {
    const primitiveResult = managedPrimitiveEquals(left, right);
    const equal = primitiveResult !== null
        ? primitiveResult
        : left?.t === 'System.Type' && right?.t === 'System.Type'
            ? unwrap(left) === unwrap(right)
            : unwrap(left) === unwrap(right);
    return wrap(equal, 'System.Boolean');
};
