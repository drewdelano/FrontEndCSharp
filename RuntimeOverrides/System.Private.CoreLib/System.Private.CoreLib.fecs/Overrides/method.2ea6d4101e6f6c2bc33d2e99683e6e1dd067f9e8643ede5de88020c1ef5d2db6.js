import { setFieldValue } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Reflection.FieldInfo.SetValue(System.Object, System.Object)'] = function (ctx, field, target, value) {
    setFieldValue(field, target, value);
};
