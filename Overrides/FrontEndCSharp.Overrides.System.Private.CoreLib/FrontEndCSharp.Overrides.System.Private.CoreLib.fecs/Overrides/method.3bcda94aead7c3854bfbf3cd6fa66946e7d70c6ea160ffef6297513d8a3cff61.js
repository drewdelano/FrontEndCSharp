import { getSharedRandom } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-random.js';
globalThis.DotNet.Methods['System.Random.get_Shared()'] = function (ctx) {
    return getSharedRandom(ctx);
};
