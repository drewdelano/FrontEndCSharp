import { getMemoryStreamCapacity } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.get_Capacity()'] = function (ctx, $this) {
    return getMemoryStreamCapacity(ctx, $this);
};
