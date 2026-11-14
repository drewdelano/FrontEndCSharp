import { memoryStreamToArray } from '../runtime-assets/System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.ToArray()'] = function (_ctx, $this) {
    return memoryStreamToArray($this);
};
