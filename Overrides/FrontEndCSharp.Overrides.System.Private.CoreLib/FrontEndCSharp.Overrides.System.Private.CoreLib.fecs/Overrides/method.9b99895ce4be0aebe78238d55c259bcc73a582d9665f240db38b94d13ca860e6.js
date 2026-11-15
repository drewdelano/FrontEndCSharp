import { nextRandomRange } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random.Next(System.Int32, System.Int32)'] = function (ctx, random, minValue, maxValue) {
    return nextRandomRange(ctx, random, minValue, maxValue);
};
