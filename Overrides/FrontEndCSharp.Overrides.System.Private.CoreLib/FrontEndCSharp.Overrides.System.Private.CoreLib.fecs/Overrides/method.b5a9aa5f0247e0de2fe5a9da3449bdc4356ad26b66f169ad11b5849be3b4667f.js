import { writeMemoryStreamByte } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.WriteByte(System.Byte)'] = function (ctx, $this, value) {
    writeMemoryStreamByte(ctx, $this, value);
};
