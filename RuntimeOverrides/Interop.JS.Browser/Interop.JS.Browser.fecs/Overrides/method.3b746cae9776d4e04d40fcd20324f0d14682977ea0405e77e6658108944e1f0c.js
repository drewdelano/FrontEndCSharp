import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.HasAttribute(System.String)'] = function (ctx, element, name) {
    return wrap(unwrap(element).hasAttribute(unwrap(name)), 'System.Boolean');
};
