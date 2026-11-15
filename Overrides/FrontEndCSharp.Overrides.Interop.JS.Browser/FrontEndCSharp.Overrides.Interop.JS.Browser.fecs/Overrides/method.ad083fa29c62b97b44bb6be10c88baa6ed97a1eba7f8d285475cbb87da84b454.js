import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.get_Id()'] = function (ctx, element) {
    return wrap(unwrap(element).id, 'System.String');
};
