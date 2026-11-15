import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.Browser.Window.QueueMicrotask(System.Action)'] = function (ctx, callback) {
    globalThis.window.queueMicrotask(() => invokeDelegate(ctx, callback));
};
