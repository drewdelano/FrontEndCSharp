import { canWriteMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.get_CanWrite()'] = function (_ctx, $this) {
    return canWriteMemoryStream($this);
};
