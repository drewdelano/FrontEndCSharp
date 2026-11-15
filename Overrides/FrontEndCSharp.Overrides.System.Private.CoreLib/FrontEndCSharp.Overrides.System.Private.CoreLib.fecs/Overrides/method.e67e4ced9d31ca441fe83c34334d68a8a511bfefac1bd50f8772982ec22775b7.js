import { secureVersion4Guid } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/secure-random.js';

globalThis.DotNet.Methods['System.Guid.NewGuid()'] = function (ctx) {
    return secureVersion4Guid(ctx);
};
