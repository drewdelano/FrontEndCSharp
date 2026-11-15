import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpRequestHeaders.get_Accept()'] = function (ctx, $this) {
    const headers = unwrap($this);
    if (headers.$fecsAccept !== undefined) {
        return headers.$fecsAccept;
    }

    const collection = initializeObject(
        'System.Net.Http.Headers.HttpHeaderValueCollection`1[T]',
        { T: 'System.Net.Http.Headers.MediaTypeWithQualityHeaderValue' });
    const collectionValue = unwrap(collection);
    collectionValue.$fecsHeaderOwner = headers;
    collectionValue.$fecsHeaderName = 'accept';
    headers.$fecsAccept = collection;
    return collection;
};
