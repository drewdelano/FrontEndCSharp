import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.set_Title(System.String)'] = function (ctx, value) {
    globalThis.document.title = unwrap(value);
};
