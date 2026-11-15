import { completeTryParse, parseEnum } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Enum.TryParse<TEnum>(System.String, TEnum&)'] = function (ctx, value, result) {
    return completeTryParse(ctx, parseEnum(ctx, value), result, 'TEnum');
};
