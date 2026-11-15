import { getEnumName } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetEnumName(System.Object)'] = function (ctx, type, value) {
    return getEnumName(type, value);
};
