import { secureRandomInt32 } from '../runtime-assets/System.Private.CoreLib.fecs/secure-random.js';

globalThis.DotNet.Methods['System.Security.Cryptography.RandomNumberGenerator.GetInt32(System.Int32, System.Int32)'] = function (ctx, fromInclusive, toExclusive) {
    return secureRandomInt32(ctx, fromInclusive, toExclusive);
};
