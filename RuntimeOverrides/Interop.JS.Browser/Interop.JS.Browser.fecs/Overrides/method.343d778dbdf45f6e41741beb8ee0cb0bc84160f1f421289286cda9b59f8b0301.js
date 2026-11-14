import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.set_TextContent(System.String)'] = function (ctx, node, value) {
    unwrap(node).textContent = value === null ? null : unwrap(value);
};
