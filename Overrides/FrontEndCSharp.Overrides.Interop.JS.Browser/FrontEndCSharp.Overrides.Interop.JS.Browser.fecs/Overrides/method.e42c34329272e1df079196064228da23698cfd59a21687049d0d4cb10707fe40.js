import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.get_LastChild()'] = function (ctx, node) {
    const child = unwrap(node).lastChild;
    return child === null ? null : wrap(child, 'Interop.JS.Browser.Node');
};
