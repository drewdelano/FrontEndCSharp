import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.get_Body()'] = function () {
    const body = globalThis.document.body;
    return body === null ? null : wrap(body, 'Interop.JS.Browser.Element');
};
