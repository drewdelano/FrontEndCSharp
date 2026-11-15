import { getTimeSpanTicks } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-temporal.js';
import { initializeObject } from '../msil.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

const tupleType = 'System.ValueTuple`3[System.Threading.CancellationTokenSource, System.Boolean, System.Threading.CancellationTokenSource]';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.PrepareCancellationTokenSource(System.Threading.CancellationToken)'] = function (ctx, $this) {
    const client = unwrap($this);
    const preparedSource = initializeObject('System.Threading.CancellationTokenSource');
    unwrap(preparedSource).$fecsHttpTimeoutMilliseconds = getTimeoutMilliseconds(client);
    return wrap({
        Item1: preparedSource,
        Item2: wrap(false, 'System.Boolean'),
        Item3: null
    }, tupleType);
};

function getTimeoutMilliseconds(client) {
    if (typeof client.$fecsTimeoutMilliseconds === 'number') {
        return client.$fecsTimeoutMilliseconds;
    }
    if (client._timeout !== null && client._timeout !== undefined) {
        return Number(getTimeSpanTicks(client._timeout)) / 10000;
    }
    return 100000;
}
