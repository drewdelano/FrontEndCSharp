import { completeTryParse, parseTemporal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.DateTimeOffset.TryParse(System.String, System.DateTimeOffset&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseTemporal(value, 'System.DateTimeOffset'), result, 'System.DateTimeOffset');
};
