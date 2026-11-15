import { setPropertyValue } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.PropertyInfo.SetValue(System.Object, System.Object)'] = function (ctx, property, target, value) {
    return setPropertyValue(ctx, property, target, value);
};
