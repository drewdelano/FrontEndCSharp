import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Node.AppendChild(Interop.JS.Browser.Node)'] = function (ctx, node, child) {
    unwrap(node).appendChild(unwrap(child));
};
