import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Int128.TryParse(System.String, System.Int128&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.Int128'), result, 'System.Int128');
};
