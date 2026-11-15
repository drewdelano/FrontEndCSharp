import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.set_ClassName(System.String)'] = function (ctx, element, value) {
    unwrap(element).className = unwrap(value);
};
