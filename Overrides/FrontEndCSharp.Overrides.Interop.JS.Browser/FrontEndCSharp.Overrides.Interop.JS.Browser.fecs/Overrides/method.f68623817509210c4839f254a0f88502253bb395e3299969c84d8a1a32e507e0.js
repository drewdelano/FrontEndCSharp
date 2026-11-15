import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.Blur()'] = function (ctx, element) {
    unwrap(element).blur();
};
