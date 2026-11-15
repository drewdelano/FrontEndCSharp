import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.get_DefaultRequestHeaders()'] = function (ctx, $this) {
    const client = unwrap($this);
    if (client.$fecsDefaultRequestHeaders !== undefined) {
        return client.$fecsDefaultRequestHeaders;
    }

    const headers = initializeObject('System.Net.Http.Headers.HttpRequestHeaders');
    unwrap(headers).$fecsHeaders = new Map();
    client.$fecsDefaultRequestHeaders = headers;
    return headers;
};
