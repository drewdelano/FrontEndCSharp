import { getPropertyValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.PropertyInfo.GetValue(System.Object)'] = function (ctx, property, target) {
    return getPropertyValue(ctx, property, target);
};
