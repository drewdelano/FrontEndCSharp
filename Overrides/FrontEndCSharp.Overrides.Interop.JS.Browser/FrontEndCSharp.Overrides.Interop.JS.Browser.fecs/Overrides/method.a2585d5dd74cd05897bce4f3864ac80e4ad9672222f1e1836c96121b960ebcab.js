import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.CreateTextNode(System.String)'] = function (ctx, data) {
    return wrap(globalThis.document.createTextNode(unwrap(data)), 'Interop.JS.Browser.Node');
};
