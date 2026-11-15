import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.HasChildNodes()'] = function (ctx, node) {
    return wrap(unwrap(node).hasChildNodes(), 'System.Boolean');
};
