import { completeTryParse, parseIntegral } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.IntPtr.TryParse(System.String, System.IntPtr&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseIntegral(value, 'System.IntPtr'), result, 'System.IntPtr');
};
