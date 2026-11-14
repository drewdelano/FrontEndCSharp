import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.CreateElement(System.String)'] = function (ctx, tagName) {
    return wrap(globalThis.document.createElement(unwrap(tagName)), 'Interop.JS.Browser.Element');
};
