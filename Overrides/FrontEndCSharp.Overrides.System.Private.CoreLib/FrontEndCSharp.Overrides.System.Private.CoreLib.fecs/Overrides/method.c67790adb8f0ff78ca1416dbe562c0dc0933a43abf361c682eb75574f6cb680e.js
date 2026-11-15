import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.UInt32.TryParse(System.String, System.UInt32&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.UInt32'), result, 'System.UInt32');
};
