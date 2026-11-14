import { addTemporalTicks } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.DateTime.AddTicks(System.Int64)'] = function (ctx, $this, ticks) {
    const value = addTemporalTicks('System.DateTime', $this, unwrap(ticks));
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The added or subtracted value results in an unrepresentable DateTime.');
    return value;
};
