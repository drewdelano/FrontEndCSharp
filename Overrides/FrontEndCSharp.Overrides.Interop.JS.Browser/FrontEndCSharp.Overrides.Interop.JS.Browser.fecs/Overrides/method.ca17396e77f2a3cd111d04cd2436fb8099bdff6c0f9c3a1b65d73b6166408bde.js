import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.SetAttribute(System.String, System.String)'] = function (ctx, element, name, value) {
    unwrap(element).setAttribute(unwrap(name), unwrap(value));
};
