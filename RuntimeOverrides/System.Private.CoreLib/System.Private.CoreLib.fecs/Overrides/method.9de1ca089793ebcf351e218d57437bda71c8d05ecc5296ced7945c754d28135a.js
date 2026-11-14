import { createTimeOnly } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { deRef, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.TimeOnly..ctor(System.Int32, System.Int32, System.Int32, System.Int32)'] = function (ctx, $this, hour, minute, second, millisecond) {
    const value = createTimeOnly(unwrap(hour), unwrap(minute), unwrap(second), unwrap(millisecond));
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The TimeOnly parameters describe an unrepresentable value.');
    Object.assign(unwrap(deRef($this)), unwrap(value));
};
