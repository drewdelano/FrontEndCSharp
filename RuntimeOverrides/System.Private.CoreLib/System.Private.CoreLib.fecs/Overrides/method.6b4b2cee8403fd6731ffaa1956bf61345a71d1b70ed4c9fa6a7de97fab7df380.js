import { isEnum } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.get_IsEnum()'] = function (ctx, type) {
    return isEnum(type);
};
