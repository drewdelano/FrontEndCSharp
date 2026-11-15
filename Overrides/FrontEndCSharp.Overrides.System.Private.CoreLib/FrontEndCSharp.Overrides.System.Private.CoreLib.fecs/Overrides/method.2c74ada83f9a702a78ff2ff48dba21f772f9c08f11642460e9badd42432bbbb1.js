import { getFieldType } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.FieldInfo.get_FieldType()'] = function (ctx, field) {
    return getFieldType(field);
};
