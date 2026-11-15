import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage.get_Headers()'] = function (ctx, $this) {
    const request = unwrap($this);
    if (request._headers !== null && request._headers !== undefined) {
        return request._headers;
    }

    const headers = initializeObject('System.Net.Http.Headers.HttpRequestHeaders');
    unwrap(headers).$fecsHeaders = new Map();
    request._headers = headers;
    return headers;
};
