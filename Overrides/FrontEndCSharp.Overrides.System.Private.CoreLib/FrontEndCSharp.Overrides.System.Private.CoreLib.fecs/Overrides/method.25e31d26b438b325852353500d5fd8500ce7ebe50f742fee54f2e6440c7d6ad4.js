import { getEnumValuesAsUnderlyingType } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetEnumValuesAsUnderlyingType()'] = function (ctx, type) {
    return getEnumValuesAsUnderlyingType(type);
};
