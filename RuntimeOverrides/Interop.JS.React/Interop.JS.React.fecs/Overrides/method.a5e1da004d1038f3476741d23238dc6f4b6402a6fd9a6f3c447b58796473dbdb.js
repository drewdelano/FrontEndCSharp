import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.React.ReactRoot.Render(Interop.JS.React.ReactNode)'] = function (ctx, root, node) {
    unwrap(root).render(unwrap(node));
};
