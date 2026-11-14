import { nextRandom } from '../runtime-assets/System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random.Next()'] = function (ctx, random) {
    return nextRandom(ctx, random);
};
