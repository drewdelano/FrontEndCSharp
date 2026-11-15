import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.InsertBefore(Interop.JS.Browser.Node, Interop.JS.Browser.Node)'] = function (ctx, node, newNode, referenceNode) {
    const inserted = unwrap(node).insertBefore(unwrap(newNode), referenceNode === null ? null : unwrap(referenceNode));
    return wrap(inserted, 'Interop.JS.Browser.Node');
};
