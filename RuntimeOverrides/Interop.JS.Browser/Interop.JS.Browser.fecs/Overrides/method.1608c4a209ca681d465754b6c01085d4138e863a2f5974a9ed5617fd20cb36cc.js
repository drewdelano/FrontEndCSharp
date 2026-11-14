import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.get_ReadyState()'] = function () {
    return wrap(globalThis.document.readyState, 'System.String');
};
