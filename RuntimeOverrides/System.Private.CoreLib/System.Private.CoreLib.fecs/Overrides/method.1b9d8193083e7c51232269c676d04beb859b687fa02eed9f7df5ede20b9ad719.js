import { completeParse, parseGuid } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Guid.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseGuid(value));
};
