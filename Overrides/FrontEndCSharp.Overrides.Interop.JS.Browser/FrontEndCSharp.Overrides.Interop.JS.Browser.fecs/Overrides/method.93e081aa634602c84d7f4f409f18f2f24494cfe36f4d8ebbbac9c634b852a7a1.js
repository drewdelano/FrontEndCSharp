import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.get_ClassName()'] = function (ctx, element) {
    return wrap(unwrap(element).className, 'System.String');
};
