import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.ClearInterval(System.Int32)'] = function (ctx, handle) {
    globalThis.window.clearInterval(unwrap(handle));
};
