import { setMemoryStreamLength } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.SetLength(System.Int64)'] = function (ctx, $this, length) {
    setMemoryStreamLength(ctx, $this, length);
};
