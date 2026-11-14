import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.Focus()'] = function (ctx, element) {
    unwrap(element).focus();
};
