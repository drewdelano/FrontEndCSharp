import { completeTryParse, parseReal } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Single.TryParse(System.String, System.Single&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseReal(value, 'System.Single'), result, 'System.Single');
};
