import { completeParse, parseIntegral } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.UInt128.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseIntegral(value, 'System.UInt128'));
};
