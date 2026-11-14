import { createDateTime } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { deRef, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.DateTime..ctor(System.Int32, System.Int32, System.Int32, System.Int32, System.Int32, System.Int32, System.Int32, System.DateTimeKind)'] = function (ctx, $this, year, month, day, hour, minute, second, millisecond, kind) {
    const value = createDateTime(
        unwrap(year), unwrap(month), unwrap(day), unwrap(hour), unwrap(minute), unwrap(second), unwrap(millisecond), unwrap(kind));
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The DateTime parameters describe an unrepresentable value.');
    Object.assign(unwrap(deRef($this)), unwrap(value));
};
