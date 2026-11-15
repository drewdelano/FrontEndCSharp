import { isEnumDefined } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.IsEnumDefined(System.Object)'] = function (ctx, type, value) {
    return isEnumDefined(type, value);
};
