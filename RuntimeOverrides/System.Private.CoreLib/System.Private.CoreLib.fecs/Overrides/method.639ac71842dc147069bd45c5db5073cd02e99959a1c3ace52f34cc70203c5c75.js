import { completeTryParse, parseIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Int16.TryParse(System.String, System.Int16&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.Int16'), result, 'System.Int16');
};
