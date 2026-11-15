import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.Click()'] = function (ctx, element) {
    unwrap(element).click();
};
