import { createTimeSpanFromTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.TimeSpan.FromMinutes(System.Double)'] = function (ctx, value) {
    const minutes = unwrap(value);
    const scaled = minutes * 600000000;
    if (!Number.isFinite(scaled)) throwManagedException(ctx, 'System.ArgumentException', 'TimeSpan does not accept floating point Not-a-Number values.');
    const ticks = BigInt(Math.trunc(scaled + (scaled < 0 ? -0.5 : 0.5)));
    const result = createTimeSpanFromTicks(ticks);
    if (result === null) throwManagedException(ctx, 'System.OverflowException', 'TimeSpan overflowed because the duration is too long.');
    return result;
};
