import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Element.QuerySelector(System.String)'] = function (ctx, element, selectors) {
    const match = unwrap(element).querySelector(unwrap(selectors));
    return match === null ? null : wrap(match, 'Interop.JS.Browser.Element');
};
