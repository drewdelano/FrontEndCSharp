import { managedPrimitiveEquals } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Collections.Generic.IEqualityComparer`1[T].Equals(T, T)'] = function (_ctx, _this, left, right) {
    if (_this?.t === 'System.OrdinalIgnoreCaseComparer'
        && left?.t === 'System.String'
        && right?.t === 'System.String') {
        return wrap(
            unwrap(left).toUpperCase() === unwrap(right).toUpperCase(),
            'System.Boolean');
    }
    const primitiveResult = managedPrimitiveEquals(left, right);
    const equal = primitiveResult !== null
        ? primitiveResult
        : left?.t === 'System.Type' && right?.t === 'System.Type'
            ? unwrap(left) === unwrap(right)
            : unwrap(left) === unwrap(right);
    return wrap(equal, 'System.Boolean');
};
