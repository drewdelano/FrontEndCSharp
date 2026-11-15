import { createTimeSpanFromTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { deRef, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.TimeSpan..ctor(System.Int32, System.Int32, System.Int32, System.Int32, System.Int32)'] = function (ctx, $this, days, hours, minutes, seconds, milliseconds) {
    const totalMilliseconds = ((((BigInt(unwrap(days)) * 24n + BigInt(unwrap(hours))) * 60n
        + BigInt(unwrap(minutes))) * 60n + BigInt(unwrap(seconds))) * 1000n) + BigInt(unwrap(milliseconds));
    const value = createTimeSpanFromTicks(totalMilliseconds * 10000n);
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'TimeSpan overflowed because the duration is too long.');
    Object.assign(unwrap(deRef($this)), unwrap(value));
};
