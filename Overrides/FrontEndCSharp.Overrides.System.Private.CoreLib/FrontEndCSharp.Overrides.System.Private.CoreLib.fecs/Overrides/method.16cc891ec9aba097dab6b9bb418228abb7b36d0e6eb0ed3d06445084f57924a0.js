import { getMemoryStreamBuffer } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.GetBuffer()'] = function (ctx, $this) {
    return getMemoryStreamBuffer(ctx, $this);
};
