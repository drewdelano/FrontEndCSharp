import { deRef, getManagedFieldValue, unwrap } from '../msil-type-helpers.js';
import { throwManagedException } from '../managed-exceptions.js';
import "./type.System.InvalidOperationException.js";
globalThis.DotNet.Methods['System.Nullable`1[T].get_Value()'] = function (ctx, thisArg) {
    const nullable = unwrap(deRef(thisArg));
    if (!unwrap(getManagedFieldValue(nullable, 'hasValue'))) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'Nullable object must have a value.');
    }

    return getManagedFieldValue(nullable, 'value');
};
