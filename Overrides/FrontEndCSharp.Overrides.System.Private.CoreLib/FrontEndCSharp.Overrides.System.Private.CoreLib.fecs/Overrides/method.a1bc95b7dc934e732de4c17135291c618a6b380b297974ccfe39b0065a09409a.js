import { completeTryParse, parseTemporal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.DateTime.TryParse(System.String, System.DateTime&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseTemporal(value, 'System.DateTime'), result, 'System.DateTime');
};
