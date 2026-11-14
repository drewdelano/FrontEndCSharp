import { secureVersion4Guid } from '../runtime-assets/System.Private.CoreLib.fecs/secure-random.js';

globalThis.DotNet.Methods['System.Guid.NewGuid()'] = function (ctx) {
    return secureVersion4Guid(ctx);
};
