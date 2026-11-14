import { disposeManagedAsyncStream } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.BrowserHttpReadStream.Dispose(System.Boolean)'] = function (ctx, $this, disposing) {
    if (unwrap(disposing)) disposeManagedAsyncStream($this);
};
