import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClient.CreateUri(System.String)'] = function (ctx, uri) {
    const value = unwrap(uri);
    if (value === null || value === '') {
        return null;
    }

    return wrap({
        _string: wrap(value, 'System.String'),
        _syntax: wrap({}, 'System.UriParser'),
        $fecsHttpUri: value
    }, 'System.Uri');
};
