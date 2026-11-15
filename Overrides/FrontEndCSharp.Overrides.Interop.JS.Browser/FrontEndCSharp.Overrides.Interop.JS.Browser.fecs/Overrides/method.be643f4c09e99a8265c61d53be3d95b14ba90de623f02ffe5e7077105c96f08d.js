import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Document.QuerySelector(System.String)'] = function (ctx, selectors) {
    const element = globalThis.document.querySelector(unwrap(selectors));
    return element === null ? null : wrap(element, 'Interop.JS.Browser.Element');
};
