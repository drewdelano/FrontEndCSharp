import { setMemoryStreamCapacity } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.set_Capacity(System.Int32)'] = function (ctx, $this, capacity) {
    setMemoryStreamCapacity(ctx, $this, capacity);
};
