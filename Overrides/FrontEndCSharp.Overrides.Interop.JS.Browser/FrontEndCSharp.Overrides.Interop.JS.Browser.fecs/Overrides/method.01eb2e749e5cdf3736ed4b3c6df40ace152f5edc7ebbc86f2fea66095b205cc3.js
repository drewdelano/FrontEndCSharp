import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.Remove()'] = function (ctx, node) {
    unwrap(node).remove();
};
