import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.get_ActiveElement()'] = function () {
    const element = globalThis.document.activeElement;
    return element === null ? null : wrap(element, 'Interop.JS.Browser.Element');
};
