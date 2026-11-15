import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.RemoveAttribute(System.String)'] = function (ctx, element, name) {
    unwrap(element).removeAttribute(unwrap(name));
};
