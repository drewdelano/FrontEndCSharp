import { seekMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.Seek(System.Int64, System.IO.SeekOrigin)'] = function (ctx, $this, offset, origin) {
    return seekMemoryStream(ctx, $this, offset, origin);
};
