import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.ArgumentOutOfRangeException.ThrowIfNegative<T>(T, System.String)'] = function (ctx, value) {
    if (Number(unwrap(value)) < 0) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Value must be non-negative.');
    }
};
