import { invokeReflectedMethod } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.MethodBase.Invoke(System.Object, System.Object[])'] = function (ctx, method, target, args) {
    return invokeReflectedMethod(ctx, method, target, args);
};
