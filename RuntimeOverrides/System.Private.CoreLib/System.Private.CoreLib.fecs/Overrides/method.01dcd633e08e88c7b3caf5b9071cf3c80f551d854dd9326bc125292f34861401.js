import { completeTryParse, parseIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.UInt16.TryParse(System.String, System.UInt16&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.UInt16'), result, 'System.UInt16');
};
