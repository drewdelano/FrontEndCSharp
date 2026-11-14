import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.GetAttribute(System.String)'] = function (ctx, element, name) {
    const value = unwrap(element).getAttribute(unwrap(name));
    return value === null ? null : wrap(value, 'System.String');
};
