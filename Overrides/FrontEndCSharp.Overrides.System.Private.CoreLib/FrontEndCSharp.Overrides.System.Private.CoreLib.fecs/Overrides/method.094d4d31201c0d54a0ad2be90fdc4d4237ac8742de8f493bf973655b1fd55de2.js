import { initializeBufferMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream..ctor(System.Byte[])'] = function (ctx, $this, buffer) {
    initializeBufferMemoryStream(ctx, $this, buffer);
};
