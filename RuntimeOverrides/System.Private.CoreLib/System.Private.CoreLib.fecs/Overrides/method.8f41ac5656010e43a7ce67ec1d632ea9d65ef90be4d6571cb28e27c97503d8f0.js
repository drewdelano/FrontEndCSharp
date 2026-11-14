import { getProperties } from '../runtime-assets/System.Private.CoreLib.fecs/reflection.js';
globalThis.DotNet.Methods['System.Type.GetProperties()'] = function (ctx, type) {
    return getProperties(type);
};
