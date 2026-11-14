import { initializeRandom } from '../runtime-assets/System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random..ctor()'] = function (ctx, random) {
    initializeRandom(ctx, random);
};
