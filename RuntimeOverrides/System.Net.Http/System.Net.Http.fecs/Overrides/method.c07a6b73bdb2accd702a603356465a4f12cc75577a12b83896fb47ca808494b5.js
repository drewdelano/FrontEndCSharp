import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.MediaTypeHeaderValue.get_MediaType()'] = function (ctx, $this) {
    const value = unwrap($this).$fecsMediaType;
    return value === null || value === undefined ? null : wrap(value, 'System.String');
};
