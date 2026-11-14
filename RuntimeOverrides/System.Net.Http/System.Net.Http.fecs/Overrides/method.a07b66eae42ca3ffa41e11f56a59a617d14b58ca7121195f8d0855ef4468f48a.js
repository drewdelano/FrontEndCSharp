import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpClientHandler.Dispose(System.Boolean)'] = function (ctx, $this, disposing) {
    if (unwrap(disposing)) {
        unwrap($this).$fecsDisposed = true;
    }
};
