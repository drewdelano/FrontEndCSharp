import { completeTryParse, parseReal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Double.TryParse(System.String, System.Double&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseReal(value, 'System.Double'), result, 'System.Double');
};
