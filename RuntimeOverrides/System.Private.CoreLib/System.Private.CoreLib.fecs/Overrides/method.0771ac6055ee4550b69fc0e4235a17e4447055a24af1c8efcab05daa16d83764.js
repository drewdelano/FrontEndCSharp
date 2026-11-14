import { completeParse, parseEnum } from '../runtime-assets/System.Private.CoreLib.fecs/managed-parsing.js';

globalThis.DotNet.Methods['System.Enum.Parse<TEnum>(System.String)'] = function (ctx, value) {
    return completeParse(ctx, parseEnum(ctx, value), 'System.ArgumentException');
};
