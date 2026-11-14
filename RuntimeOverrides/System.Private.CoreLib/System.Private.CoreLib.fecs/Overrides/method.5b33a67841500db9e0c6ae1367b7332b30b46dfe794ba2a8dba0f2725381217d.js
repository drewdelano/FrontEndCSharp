import { getMemoryStreamLength } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.get_Length()'] = function (ctx, $this) {
    return getMemoryStreamLength(ctx, $this);
};
