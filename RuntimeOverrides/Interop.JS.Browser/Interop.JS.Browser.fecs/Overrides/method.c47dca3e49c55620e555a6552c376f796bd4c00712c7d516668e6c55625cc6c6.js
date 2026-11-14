import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.get_ParentNode()'] = function (ctx, node) {
    const parent = unwrap(node).parentNode;
    return parent === null ? null : wrap(parent, 'Interop.JS.Browser.Node');
};
