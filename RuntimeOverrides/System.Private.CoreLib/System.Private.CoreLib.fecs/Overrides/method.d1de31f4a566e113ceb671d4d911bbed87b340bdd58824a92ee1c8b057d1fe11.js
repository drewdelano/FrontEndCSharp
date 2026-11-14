import { getMethods } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetMethods()'] = function (ctx, type) {
    return getMethods(type);
};
