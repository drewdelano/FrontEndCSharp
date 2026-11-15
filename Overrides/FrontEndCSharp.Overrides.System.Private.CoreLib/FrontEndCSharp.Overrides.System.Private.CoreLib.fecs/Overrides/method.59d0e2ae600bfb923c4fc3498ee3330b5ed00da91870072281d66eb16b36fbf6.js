import { getFields } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetFields()'] = function (ctx, type) {
    return getFields(type);
};
