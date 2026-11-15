import { writeManagedMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.Write(System.Byte[], System.Int32, System.Int32)'] = function (ctx, $this, buffer, offset, count) {
    writeManagedMemoryStream(ctx, $this, buffer, offset, count);
};
