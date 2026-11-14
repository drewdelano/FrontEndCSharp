import { completeParse, parseReal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Double.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseReal(value, 'System.Double'));
};
