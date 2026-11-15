import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Int64.TryParse(System.String, System.Int64&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.Int64'), result, 'System.Int64');
};
