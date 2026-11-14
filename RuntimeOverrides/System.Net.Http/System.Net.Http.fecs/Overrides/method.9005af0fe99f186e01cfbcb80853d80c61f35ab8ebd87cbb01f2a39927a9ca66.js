import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpRequestHeaders.set_Authorization(System.Net.Http.Headers.AuthenticationHeaderValue)'] = function (ctx, $this, value) {
    const headers = unwrap($this);
    headers.$fecsHeaders ??= new Map();
    const headerValue = unwrap(value);
    if (headerValue === null || headerValue === undefined) {
        headers.$fecsHeaders.delete('authorization');
        return;
    }
    headers.$fecsHeaders.set('authorization', [headerValue.$fecsHeaderValue]);
};
