import { completeTryParse, parseGuid } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Guid.TryParse(System.String, System.Guid&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseGuid(value), result, 'System.Guid');
};
