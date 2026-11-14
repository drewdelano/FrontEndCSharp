import { primitiveGetHashCode } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Enum.GetHashCode()'] = function (ctx, $this) {
    return primitiveGetHashCode(ctx, $this);
};
