import { getEnumValuesAsUnderlyingType } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetEnumValuesAsUnderlyingType()'] = function (ctx, type) {
    return getEnumValuesAsUnderlyingType(type);
};
