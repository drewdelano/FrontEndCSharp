import { getTimeSpanTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.set_Timeout(System.TimeSpan)'] = function (ctx, $this, value) {
    const client = unwrap($this);
    if (unwrap(client._disposed)) {
        throwManagedException(ctx, 'System.ObjectDisposedException', 'Cannot access a disposed HttpClient.');
    }
    if (unwrap(client._operationStarted)) {
        throwManagedException(
            ctx,
            'System.InvalidOperationException',
            'Properties can only be modified before sending the first request.');
    }

    const ticks = getTimeSpanTicks(value);
    if (ticks !== -10000n && (ticks <= 0n || ticks > 21474836470000n)) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            'The timeout must be greater than zero and no greater than Int32.MaxValue milliseconds.');
    }
    client._timeout = value;
    client.$fecsTimeout = value;
    client.$fecsTimeoutMilliseconds = Number(ticks) / 10000;
};
