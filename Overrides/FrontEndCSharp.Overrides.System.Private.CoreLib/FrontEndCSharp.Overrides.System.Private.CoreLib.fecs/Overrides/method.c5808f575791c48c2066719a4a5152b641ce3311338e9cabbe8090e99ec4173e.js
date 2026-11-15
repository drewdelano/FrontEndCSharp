import { completeTryParse, parseReal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Half.TryParse(System.String, System.Half&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseReal(value, 'System.Half'), result, 'System.Half');
};
