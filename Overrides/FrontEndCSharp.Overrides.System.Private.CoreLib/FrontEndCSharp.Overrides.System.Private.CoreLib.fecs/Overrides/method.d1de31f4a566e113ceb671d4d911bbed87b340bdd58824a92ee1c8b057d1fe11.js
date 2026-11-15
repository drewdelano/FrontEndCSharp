import { getMethods } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetMethods()'] = function (ctx, type) {
    return getMethods(type);
};
