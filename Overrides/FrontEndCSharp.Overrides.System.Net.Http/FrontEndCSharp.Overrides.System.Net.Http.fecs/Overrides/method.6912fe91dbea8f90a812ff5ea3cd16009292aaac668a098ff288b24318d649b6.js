import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpMessageInvoker..ctor(System.Net.Http.HttpMessageHandler, System.Boolean)'] = function (ctx, $this, handler, disposeHandler) {
    const instance = unwrap($this);
    instance._handler = handler;
    instance._disposeHandler = disposeHandler;
    instance._disposed = false;
};
