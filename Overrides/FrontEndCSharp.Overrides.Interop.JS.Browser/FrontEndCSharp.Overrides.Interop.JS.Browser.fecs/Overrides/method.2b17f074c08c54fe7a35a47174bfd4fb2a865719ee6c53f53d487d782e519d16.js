import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.get_TagName()'] = function (ctx, element) {
    return wrap(unwrap(element).tagName, 'System.String');
};
