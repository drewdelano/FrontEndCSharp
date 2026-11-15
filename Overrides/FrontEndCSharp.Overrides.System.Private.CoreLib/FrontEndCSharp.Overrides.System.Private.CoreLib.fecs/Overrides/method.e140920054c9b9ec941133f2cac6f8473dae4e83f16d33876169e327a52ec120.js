import { initializeRandom } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random..ctor(System.Int32)'] = function (ctx, random, seed) {
    initializeRandom(ctx, random, seed);
};
