import { disposeMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream.Dispose(System.Boolean)'] = function (_ctx, $this, _disposing) {
    disposeMemoryStream($this);
};
