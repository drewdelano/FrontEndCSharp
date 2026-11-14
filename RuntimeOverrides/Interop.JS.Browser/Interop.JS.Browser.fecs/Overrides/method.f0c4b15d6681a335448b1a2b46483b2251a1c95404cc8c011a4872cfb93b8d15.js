import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.set_InnerText(System.String)'] = function (ctx, element, value) {
    unwrap(element).innerText = unwrap(value);
};
