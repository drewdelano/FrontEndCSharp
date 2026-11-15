import { primitiveGetHashCode } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Boolean.GetHashCode()'] = function (ctx, $this) {
    return primitiveGetHashCode(ctx, $this);
};
