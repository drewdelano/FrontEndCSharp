import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.get_InnerText()'] = function (ctx, element) {
    return wrap(unwrap(element).innerText, 'System.String');
};
