import { canReadMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.get_CanRead()'] = function (_ctx, $this) {
    return canReadMemoryStream($this);
};
