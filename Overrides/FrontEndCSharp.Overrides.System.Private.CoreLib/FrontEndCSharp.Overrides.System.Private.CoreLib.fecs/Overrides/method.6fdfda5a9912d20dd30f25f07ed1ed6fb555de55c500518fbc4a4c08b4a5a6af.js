import { primitiveGetHashCode } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.UInt64.GetHashCode()'] = function (ctx, $this) {
    return primitiveGetHashCode(ctx, $this);
};
