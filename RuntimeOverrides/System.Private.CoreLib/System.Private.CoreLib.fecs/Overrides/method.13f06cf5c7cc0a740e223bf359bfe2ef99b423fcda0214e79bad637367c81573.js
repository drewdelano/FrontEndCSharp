import { nextRandomDouble } from '../runtime-assets/System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random.NextDouble()'] = function (ctx, random) {
    return nextRandomDouble(ctx, random);
};
