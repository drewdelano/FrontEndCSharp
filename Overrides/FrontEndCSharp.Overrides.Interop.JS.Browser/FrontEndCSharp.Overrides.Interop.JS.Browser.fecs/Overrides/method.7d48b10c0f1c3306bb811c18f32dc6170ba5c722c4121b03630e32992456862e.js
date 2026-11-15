import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.get_InnerHtml()'] = function (ctx, element) {
    return wrap(unwrap(element).innerHTML, 'System.String');
};
