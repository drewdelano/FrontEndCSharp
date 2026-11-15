import { completeTryParse, parseTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.DateOnly.TryParse(System.String, System.DateOnly&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseTemporal(value, 'System.DateOnly'), result, 'System.DateOnly');
};
