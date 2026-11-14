import { completeTryParse, parseIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.UInt64.TryParse(System.String, System.UInt64&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.UInt64'), result, 'System.UInt64');
};
