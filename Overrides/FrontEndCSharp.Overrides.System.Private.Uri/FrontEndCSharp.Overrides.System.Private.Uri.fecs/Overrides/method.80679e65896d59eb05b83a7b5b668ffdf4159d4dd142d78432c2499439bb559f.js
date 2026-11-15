import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Uri.get_IsAbsoluteUri()'] = function (ctx, $this) {
    return wrap(unwrap($this).$fecsIsAbsolute === true, 'System.Boolean');
};
