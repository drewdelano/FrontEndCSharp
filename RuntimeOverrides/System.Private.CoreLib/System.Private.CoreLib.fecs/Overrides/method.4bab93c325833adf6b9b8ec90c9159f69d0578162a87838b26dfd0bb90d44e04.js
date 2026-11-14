import { getMemoryStreamPosition } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.get_Position()'] = function (ctx, $this) {
    return getMemoryStreamPosition(ctx, $this);
};
