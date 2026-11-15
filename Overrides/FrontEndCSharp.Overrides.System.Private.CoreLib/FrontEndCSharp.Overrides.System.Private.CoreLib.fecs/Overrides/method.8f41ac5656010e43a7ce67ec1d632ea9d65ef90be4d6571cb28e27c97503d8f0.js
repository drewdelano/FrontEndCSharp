import { getProperties } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetProperties()'] = function (ctx, type) {
    return getProperties(type);
};
