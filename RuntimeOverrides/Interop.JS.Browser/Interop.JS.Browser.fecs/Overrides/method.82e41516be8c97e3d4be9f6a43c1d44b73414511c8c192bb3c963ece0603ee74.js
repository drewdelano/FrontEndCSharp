import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.GetElementById(System.String)'] = function (ctx, id) {
    const element = globalThis.document.getElementById(unwrap(id));
    return element === null ? null : wrap(element, 'Interop.JS.Browser.Element');
};
