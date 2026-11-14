import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.get_Title()'] = function () {
    return wrap(globalThis.document.title, 'System.String');
};
