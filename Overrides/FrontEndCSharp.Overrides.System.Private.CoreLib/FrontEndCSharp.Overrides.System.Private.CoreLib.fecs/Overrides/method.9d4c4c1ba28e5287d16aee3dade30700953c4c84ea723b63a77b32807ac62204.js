import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.UIntPtr.TryParse(System.String, System.UIntPtr&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.UIntPtr'), result, 'System.UIntPtr');
};
