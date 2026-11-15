import { completeTryParse, parseTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.TimeSpan.TryParse(System.String, System.TimeSpan&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseTemporal(value, 'System.TimeSpan'), result, 'System.TimeSpan');
};
