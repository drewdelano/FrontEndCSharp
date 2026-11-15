import { unwrap, wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.RequestAnimationFrame(System.Action`1[T])'] = function (ctx, callback) {
    const handle = globalThis.window.requestAnimationFrame(timestamp => {
        invokeDelegate(ctx, callback, [wrap(timestamp, 'System.Double')]);
    });
    return wrap(handle, 'System.Int32');
};
