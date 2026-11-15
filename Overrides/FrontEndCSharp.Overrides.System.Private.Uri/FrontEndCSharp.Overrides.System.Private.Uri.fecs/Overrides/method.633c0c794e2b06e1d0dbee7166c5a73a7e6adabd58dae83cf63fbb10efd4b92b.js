import { throwManagedException } from '../managed-exceptions.js';
import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Uri..ctor(System.String)'] = function (ctx, $this, uriString) {
    const value = uriString?.v;
    if (value === null || value === undefined) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'URI string cannot be null.');
    }

    const uri = $this.v;
    uri._string = wrap(String(value), 'System.String');
    uri._syntax = /^https?:\/\//i.test(String(value))
        ? wrap({}, 'System.UriParser')
        : null;
    uri.$fecsHttpUri = String(value);
    uri.$fecsIsAbsolute = /^[A-Za-z][A-Za-z0-9+.-]*:/.test(String(value));
};
