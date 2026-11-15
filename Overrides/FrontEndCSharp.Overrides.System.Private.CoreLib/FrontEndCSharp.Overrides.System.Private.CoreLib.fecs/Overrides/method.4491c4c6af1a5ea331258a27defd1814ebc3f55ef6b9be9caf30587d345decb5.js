import { isValueType } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';

globalThis.DotNet.Methods['System.Type.get_IsValueType()'] = function (_ctx, type) {
    return isValueType(type);
};
