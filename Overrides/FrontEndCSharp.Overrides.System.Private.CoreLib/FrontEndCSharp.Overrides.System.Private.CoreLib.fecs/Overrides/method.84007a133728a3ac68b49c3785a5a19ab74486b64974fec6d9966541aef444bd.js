import { tryGetMemoryStreamBuffer } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.TryGetBuffer(System.ArraySegment`1[T]&)'] = function (_ctx, $this, segment) {
    return tryGetMemoryStreamBuffer($this, segment);
};
