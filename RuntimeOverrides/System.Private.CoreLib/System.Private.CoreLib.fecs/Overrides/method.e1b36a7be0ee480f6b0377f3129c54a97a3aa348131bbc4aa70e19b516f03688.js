import { primitiveGetHashCode } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Double.GetHashCode()'] = function (ctx, $this) {
    return primitiveGetHashCode(ctx, $this);
};
