import { writeManagedAsyncStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.Stream.WriteAsync(System.Byte[], System.Int32, System.Int32, System.Threading.CancellationToken)'] = function (ctx, $this, buffer, offset, count, cancellationToken) {
    return writeManagedAsyncStream(ctx, $this, buffer, offset, count, cancellationToken);
};
