import { createDateTimeOffset } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { deRef, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.DateTimeOffset..ctor(System.Int32, System.Int32, System.Int32, System.Int32, System.Int32, System.Int32, System.Int32, System.TimeSpan)'] = function (ctx, $this, year, month, day, hour, minute, second, millisecond, offset) {
    const value = createDateTimeOffset(
        unwrap(year), unwrap(month), unwrap(day), unwrap(hour), unwrap(minute), unwrap(second), unwrap(millisecond), offset);
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The DateTimeOffset parameters describe an unrepresentable value.');
    Object.assign(unwrap(deRef($this)), unwrap(value));
};
