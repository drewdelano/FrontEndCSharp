import { secureRandomInt32Below } from '../runtime-assets/System.Private.CoreLib.fecs/secure-random.js';

globalThis.DotNet.Methods['System.Security.Cryptography.RandomNumberGenerator.GetInt32(System.Int32)'] = function (ctx, toExclusive) {
    return secureRandomInt32Below(ctx, toExclusive);
};
