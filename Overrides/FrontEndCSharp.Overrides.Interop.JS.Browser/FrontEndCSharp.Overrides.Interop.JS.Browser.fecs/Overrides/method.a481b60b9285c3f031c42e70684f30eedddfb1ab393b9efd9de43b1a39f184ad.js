import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.get_TextContent()'] = function (ctx, node) {
    const value = unwrap(node).textContent;
    return value === null ? null : wrap(value, 'System.String');
};
