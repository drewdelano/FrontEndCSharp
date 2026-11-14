import { createTimeSpanFromTicks } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.get_Timeout()'] = function (ctx, $this) {
    const client = unwrap($this);
    if (client.$fecsTimeout !== undefined) return client.$fecsTimeout;
    return createTimeSpanFromTicks(1000000000n);
};
