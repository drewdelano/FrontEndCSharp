import { fillRandomBytes } from '../runtime-assets/System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random.NextBytes(System.Byte[])'] = function (ctx, random, buffer) {
    fillRandomBytes(ctx, random, buffer);
};
