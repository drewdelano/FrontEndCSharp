import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.get_IsConnected()'] = function (ctx, node) {
    return wrap(unwrap(node).isConnected, 'System.Boolean');
};
