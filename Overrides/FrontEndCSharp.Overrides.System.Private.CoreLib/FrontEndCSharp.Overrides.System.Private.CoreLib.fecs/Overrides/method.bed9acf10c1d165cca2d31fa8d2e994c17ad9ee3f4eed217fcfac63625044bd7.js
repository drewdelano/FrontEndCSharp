import { canSeekMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.get_CanSeek()'] = function (_ctx, $this) {
    return canSeekMemoryStream($this);
};
