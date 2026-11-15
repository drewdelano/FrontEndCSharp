import { objectGetHashCode } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Object.GetHashCode()'] = function (ctx, $this) {
    return objectGetHashCode(ctx, $this);
};
