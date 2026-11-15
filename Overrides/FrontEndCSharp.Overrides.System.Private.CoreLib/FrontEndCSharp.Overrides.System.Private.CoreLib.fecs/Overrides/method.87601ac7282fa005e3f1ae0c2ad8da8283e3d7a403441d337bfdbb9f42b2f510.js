import { getMethodFromHandle } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.MethodBase.GetMethodFromHandle(System.RuntimeMethodHandle)'] = function (ctx, handle) {
    return getMethodFromHandle(handle);
};
