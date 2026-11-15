import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.set_InnerHtml(System.String)'] = function (ctx, element, value) {
    unwrap(element).innerHTML = unwrap(value);
};
