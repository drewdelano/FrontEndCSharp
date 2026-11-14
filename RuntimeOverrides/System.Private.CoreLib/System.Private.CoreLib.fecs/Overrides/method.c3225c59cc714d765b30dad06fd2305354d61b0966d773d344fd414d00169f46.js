import { readMemoryStreamByte } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.ReadByte()'] = function (ctx, $this) {
    return readMemoryStreamByte(ctx, $this);
};
