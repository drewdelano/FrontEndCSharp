import { getEnumUnderlyingType } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetEnumUnderlyingType()'] = function (ctx, type) {
    return getEnumUnderlyingType(type);
};
