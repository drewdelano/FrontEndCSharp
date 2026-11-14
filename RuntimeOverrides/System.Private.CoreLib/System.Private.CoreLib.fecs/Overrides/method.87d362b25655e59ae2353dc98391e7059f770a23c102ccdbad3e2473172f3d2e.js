import { completeTryParse, parseIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Int32.TryParse(System.String, System.Int32&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.Int32'), result, 'System.Int32');
};
