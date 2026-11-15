import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.SByte.TryParse(System.String, System.SByte&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.SByte'), result, 'System.SByte');
};
