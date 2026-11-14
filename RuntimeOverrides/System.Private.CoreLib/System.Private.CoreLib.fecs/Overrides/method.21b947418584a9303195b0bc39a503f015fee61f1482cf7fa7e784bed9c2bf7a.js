import { completeParse, parseTemporal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.DateTime.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseTemporal(value, 'System.DateTime'));
};
