import { getPropertyType } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.PropertyInfo.get_PropertyType()'] = function (ctx, property) {
    return getPropertyType(property);
};
