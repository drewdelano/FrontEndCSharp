import { completeTryParse, parseReal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Decimal.TryParse(System.String, System.Decimal&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseReal(value, 'System.Decimal'), result, 'System.Decimal');
};
