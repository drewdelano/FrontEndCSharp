import { completeParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Int64.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseIntegral(value, 'System.Int64'));
};
