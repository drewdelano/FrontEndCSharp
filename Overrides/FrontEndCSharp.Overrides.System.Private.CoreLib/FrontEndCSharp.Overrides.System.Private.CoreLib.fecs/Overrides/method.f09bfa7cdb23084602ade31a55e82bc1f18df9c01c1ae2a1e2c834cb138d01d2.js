import { completeTryParse, parseTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.TimeOnly.TryParse(System.String, System.TimeOnly&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseTemporal(value, 'System.TimeOnly'), result, 'System.TimeOnly');
};
