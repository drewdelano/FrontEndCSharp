import { disposeManagedAsyncStream } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';

globalThis.DotNet.Methods['System.IO.Stream.Dispose()'] = function (ctx, $this) {
    disposeManagedAsyncStream($this);
};
