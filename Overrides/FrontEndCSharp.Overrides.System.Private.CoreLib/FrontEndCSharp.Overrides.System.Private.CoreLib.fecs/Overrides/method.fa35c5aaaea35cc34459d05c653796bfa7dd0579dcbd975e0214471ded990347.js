import { primitiveGetHashCode } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Single.GetHashCode()'] = function (ctx, $this) {
    return primitiveGetHashCode(ctx, $this);
};
