import { getFieldValue } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.FieldInfo.GetValue(System.Object)'] = function (ctx, field, target) {
    return getFieldValue(field, target);
};
