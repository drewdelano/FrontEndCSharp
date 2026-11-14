import { getEnumValues } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetEnumValues()'] = function (ctx, type) {
    return getEnumValues(type);
};
