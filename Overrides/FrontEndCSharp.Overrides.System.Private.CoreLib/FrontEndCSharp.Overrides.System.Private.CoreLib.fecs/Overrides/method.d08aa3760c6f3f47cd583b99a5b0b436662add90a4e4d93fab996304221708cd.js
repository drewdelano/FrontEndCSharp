import { addTemporalTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.DateTimeOffset.AddTicks(System.Int64)'] = function (ctx, $this, ticks) {
    const value = addTemporalTicks('System.DateTimeOffset', $this, unwrap(ticks));
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The added or subtracted value results in an unrepresentable DateTimeOffset.');
    return value;
};
