import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpMessageInvoker.Dispose()'] = function (ctx, $this) {
    const invoker = unwrap($this);
    if (unwrap(invoker._disposed)) {
        return;
    }

    invoker._disposed = true;
    if (unwrap(invoker._disposeHandler)) {
        const handler = unwrap(invoker._handler);
        if (handler) {
            handler.$fecsDisposed = true;
        }
    }
};
