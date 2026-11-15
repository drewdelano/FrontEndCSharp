import { initializeEmptyMemoryStream } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-stream.js';
globalThis.DotNet.Methods['System.IO.MemoryStream..ctor()'] = function (_ctx, $this) {
    initializeEmptyMemoryStream($this);
};
