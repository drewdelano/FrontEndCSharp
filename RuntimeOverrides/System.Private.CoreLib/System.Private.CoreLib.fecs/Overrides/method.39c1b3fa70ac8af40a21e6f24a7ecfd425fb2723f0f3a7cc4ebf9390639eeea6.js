import { readManagedAsyncStream } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';

globalThis.DotNet.Methods['System.IO.Stream.ReadAsync(System.Byte[], System.Int32, System.Int32)'] = function (ctx, $this, buffer, offset, count) {
    return readManagedAsyncStream(ctx, $this, buffer, offset, count);
};
