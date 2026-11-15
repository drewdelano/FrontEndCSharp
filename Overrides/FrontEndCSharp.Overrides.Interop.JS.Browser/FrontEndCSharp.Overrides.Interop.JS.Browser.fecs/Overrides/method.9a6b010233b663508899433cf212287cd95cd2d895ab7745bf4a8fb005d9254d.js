import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.get_DocumentElement()'] = function () {
    return wrap(globalThis.document.documentElement, 'Interop.JS.Browser.Element');
};
