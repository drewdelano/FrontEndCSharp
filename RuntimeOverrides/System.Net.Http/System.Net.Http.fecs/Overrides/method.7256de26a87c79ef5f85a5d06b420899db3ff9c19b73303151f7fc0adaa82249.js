import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.Headers.HttpContentHeaders.get_ContentType()'] = function (ctx, $this) {
    const headers = unwrap($this);
    if (headers.$fecsContentType !== undefined) return headers.$fecsContentType;
    const rawValue = headers.$fecsHeaders?.get('content-type')?.[0];
    if (rawValue === undefined) {
        headers.$fecsContentType = null;
        return null;
    }

    const parts = String(rawValue).split(';');
    const contentType = initializeObject('System.Net.Http.Headers.MediaTypeHeaderValue');
    const contentTypeValue = unwrap(contentType);
    contentTypeValue.$fecsMediaType = parts[0].trim();
    contentTypeValue.$fecsCharSet = null;
    for (const parameter of parts.slice(1)) {
        const separator = parameter.indexOf('=');
        if (separator < 0) continue;
        const name = parameter.slice(0, separator).trim().toLowerCase();
        if (name !== 'charset') continue;
        const value = parameter.slice(separator + 1).trim();
        contentTypeValue.$fecsCharSet = value.length >= 2
            && value.startsWith('"')
            && value.endsWith('"')
                ? value.slice(1, -1)
                : value;
    }
    headers.$fecsContentType = contentType;
    return contentType;
};
