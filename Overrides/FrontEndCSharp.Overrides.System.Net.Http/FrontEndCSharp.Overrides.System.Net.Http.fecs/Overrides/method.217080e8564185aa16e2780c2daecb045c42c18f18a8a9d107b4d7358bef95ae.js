import { initializeObject } from '../msil.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpContent.get_Headers()'] = function (ctx, $this) {
    const content = unwrap($this);
    if (content._headers !== null && content._headers !== undefined) {
        return content._headers;
    }

    const headers = initializeObject('System.Net.Http.Headers.HttpContentHeaders');
    const headersValue = unwrap(headers);
    headersValue._parent = $this;
    headersValue.$fecsHeaders = content.$fecsHeaders ?? new Map();
    content._headers = headers;
    return headers;
};
