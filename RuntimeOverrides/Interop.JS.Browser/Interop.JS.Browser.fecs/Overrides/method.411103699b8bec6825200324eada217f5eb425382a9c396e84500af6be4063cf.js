import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.CancelAnimationFrame(System.Int32)'] = function (ctx, handle) {
    globalThis.window.cancelAnimationFrame(unwrap(handle));
};
