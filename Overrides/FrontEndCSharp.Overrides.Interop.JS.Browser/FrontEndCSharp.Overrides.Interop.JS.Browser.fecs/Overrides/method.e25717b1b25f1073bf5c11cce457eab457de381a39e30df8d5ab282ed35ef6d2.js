import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.get_Head()'] = function () {
    return wrap(globalThis.document.head, 'Interop.JS.Browser.Element');
};
