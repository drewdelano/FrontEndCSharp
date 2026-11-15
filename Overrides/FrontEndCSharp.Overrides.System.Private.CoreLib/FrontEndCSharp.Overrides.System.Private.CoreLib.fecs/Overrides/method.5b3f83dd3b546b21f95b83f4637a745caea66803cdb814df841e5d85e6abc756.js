import { primitiveEquals } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Int16.Equals(System.Object)'] = function (ctx, $this, other) {
    return primitiveEquals(ctx, $this, other);
};
