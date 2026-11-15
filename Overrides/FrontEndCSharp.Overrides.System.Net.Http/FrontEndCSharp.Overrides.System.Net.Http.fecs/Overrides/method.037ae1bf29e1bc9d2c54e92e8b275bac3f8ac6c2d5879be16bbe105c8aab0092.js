import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpResponseMessage.get_Headers()'] = function (ctx, $this) {
    const response = unwrap($this);
    if (response._headers !== null && response._headers !== undefined) {
        return response._headers;
    }

    const headers = initializeObject('System.Net.Http.Headers.HttpResponseHeaders');
    unwrap(headers).$fecsHeaders = response.$fecsHeaders ?? new Map();
    response._headers = headers;
    return headers;
};
