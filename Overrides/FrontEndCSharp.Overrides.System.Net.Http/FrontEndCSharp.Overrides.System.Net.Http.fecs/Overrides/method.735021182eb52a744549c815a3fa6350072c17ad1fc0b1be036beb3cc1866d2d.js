import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.get_BaseAddress()'] = function (ctx, $this) {
    return unwrap($this)._baseAddress ?? null;
};
