import { nextRandomBelow } from '../runtime-assets/System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random.Next(System.Int32)'] = function (ctx, random, maxValue) {
    return nextRandomBelow(ctx, random, maxValue);
};
