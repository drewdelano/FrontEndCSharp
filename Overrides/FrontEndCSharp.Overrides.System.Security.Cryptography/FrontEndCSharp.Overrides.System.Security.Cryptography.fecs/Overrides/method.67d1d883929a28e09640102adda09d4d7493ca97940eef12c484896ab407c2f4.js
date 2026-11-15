import { managedSecureRandomBytes } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/secure-random.js';

globalThis.DotNet.Methods['System.Security.Cryptography.RandomNumberGenerator.GetBytes(System.Int32)'] = function (ctx, count) {
    return managedSecureRandomBytes(ctx, count);
};
