import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.QuerySelectorAll(System.String)'] = function (ctx, selectors) {
    const elements = Array.from(
        globalThis.document.querySelectorAll(unwrap(selectors)),
        element => wrap(element, 'Interop.JS.Browser.Element'));
    return wrap(elements, 'Interop.JS.Browser.Element[]');
};
