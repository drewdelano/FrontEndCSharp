import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.RemoveChild(Interop.JS.Browser.Node)'] = function (ctx, node, child) {
    return wrap(unwrap(node).removeChild(unwrap(child)), 'Interop.JS.Browser.Node');
};
