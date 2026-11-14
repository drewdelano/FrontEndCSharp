import { setMemoryStreamPosition } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.set_Position(System.Int64)'] = function (ctx, $this, position) {
    setMemoryStreamPosition(ctx, $this, position);
};
