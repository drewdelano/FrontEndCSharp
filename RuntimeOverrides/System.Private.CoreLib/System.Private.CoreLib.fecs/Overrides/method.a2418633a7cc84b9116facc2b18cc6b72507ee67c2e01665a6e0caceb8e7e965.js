import { getEnumNames } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetEnumNames()'] = function (ctx, type) {
    return getEnumNames(type);
};
