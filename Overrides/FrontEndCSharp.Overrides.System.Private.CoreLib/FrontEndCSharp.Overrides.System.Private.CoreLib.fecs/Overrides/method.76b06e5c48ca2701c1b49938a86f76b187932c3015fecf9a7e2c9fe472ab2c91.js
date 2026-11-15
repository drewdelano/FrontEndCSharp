import { completeParse, parseTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.DateTimeOffset.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseTemporal(value, 'System.DateTimeOffset'));
};
