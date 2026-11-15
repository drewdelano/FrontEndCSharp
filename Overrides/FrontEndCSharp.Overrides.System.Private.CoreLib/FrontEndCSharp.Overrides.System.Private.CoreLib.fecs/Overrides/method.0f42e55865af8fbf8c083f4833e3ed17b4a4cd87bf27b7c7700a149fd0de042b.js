import { addTemporalTicks, getTimeSpanTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.TimeSpan.Add(System.TimeSpan)'] = function (ctx, $this, value) {
    const result = addTemporalTicks('System.TimeSpan', $this, getTimeSpanTicks(value));
    if (result === null) throwManagedException(ctx, 'System.OverflowException', 'TimeSpan overflowed because the duration is too long.');
    return result;
};
