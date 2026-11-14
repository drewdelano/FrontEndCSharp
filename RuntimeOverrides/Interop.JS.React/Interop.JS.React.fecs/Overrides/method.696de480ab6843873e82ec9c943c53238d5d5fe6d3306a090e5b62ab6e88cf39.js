import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.React.ReactRoot.Unmount()'] = function (ctx, root) {
    unwrap(root).unmount();
};
