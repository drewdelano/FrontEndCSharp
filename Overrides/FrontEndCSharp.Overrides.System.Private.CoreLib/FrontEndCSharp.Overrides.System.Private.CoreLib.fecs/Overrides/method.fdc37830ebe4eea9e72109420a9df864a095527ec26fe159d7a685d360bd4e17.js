import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Byte.TryParse(System.String, System.Byte&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.Byte'), result, 'System.Byte');
};
