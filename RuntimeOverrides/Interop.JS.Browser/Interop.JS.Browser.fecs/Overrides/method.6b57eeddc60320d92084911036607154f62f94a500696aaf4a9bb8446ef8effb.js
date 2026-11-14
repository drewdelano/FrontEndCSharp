import { unwrap, wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.SetTimeout(System.Action, System.Int32)'] = function (ctx, callback, milliseconds) {
    const handle = globalThis.window.setTimeout(() => invokeDelegate(ctx, callback), unwrap(milliseconds));
    return wrap(handle, 'System.Int32');
};
