import { readManagedAsyncStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';

globalThis.DotNet.Methods['System.Net.Http.BrowserHttpReadStream.ReadAsync(System.Byte[], System.Int32, System.Int32, System.Threading.CancellationToken)'] = function (ctx, $this, buffer, offset, count, cancellationToken) {
    return readManagedAsyncStream(ctx, $this, buffer, offset, count, cancellationToken);
};
