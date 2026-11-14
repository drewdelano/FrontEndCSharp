import { completeTryParse, parseIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.UInt128.TryParse(System.String, System.UInt128&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.UInt128'), result, 'System.UInt128');
};
