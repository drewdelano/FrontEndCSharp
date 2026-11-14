import { readManagedMemoryStream } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';

globalThis.DotNet.Methods['System.IO.MemoryStream.Read(System.Byte[], System.Int32, System.Int32)'] = function (ctx, $this, buffer, offset, count) {
    return readManagedMemoryStream(ctx, $this, buffer, offset, count);
};
