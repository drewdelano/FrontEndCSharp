import { getMethodReturnType } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.MethodInfo.get_ReturnType()'] = function (ctx, method) {
    return getMethodReturnType(method);
};
