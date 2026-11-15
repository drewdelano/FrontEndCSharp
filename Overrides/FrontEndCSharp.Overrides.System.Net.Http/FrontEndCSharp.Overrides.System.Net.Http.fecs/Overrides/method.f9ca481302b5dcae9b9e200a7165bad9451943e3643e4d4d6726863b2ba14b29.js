import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.MediaTypeHeaderValue.get_CharSet()'] = function (ctx, $this) {
    const value = unwrap($this).$fecsCharSet;
    return value === null || value === undefined ? null : wrap(value, 'System.String');
};
