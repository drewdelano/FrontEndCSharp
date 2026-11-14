import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.ClearTimeout(System.Int32)'] = function (ctx, handle) {
    globalThis.window.clearTimeout(unwrap(handle));
};
