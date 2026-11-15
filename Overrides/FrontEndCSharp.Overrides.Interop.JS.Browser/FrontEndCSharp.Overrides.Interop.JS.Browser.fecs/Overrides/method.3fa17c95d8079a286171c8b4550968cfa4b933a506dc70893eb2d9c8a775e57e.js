import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.set_Id(System.String)'] = function (ctx, element, value) {
    unwrap(element).id = unwrap(value);
};
