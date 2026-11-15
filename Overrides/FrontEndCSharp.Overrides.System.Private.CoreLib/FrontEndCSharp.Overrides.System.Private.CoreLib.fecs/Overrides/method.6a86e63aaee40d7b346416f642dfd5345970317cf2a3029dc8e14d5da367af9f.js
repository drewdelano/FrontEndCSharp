import { completeParse, parseReal } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Half.Parse(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseReal(value, 'System.Half'));
};
