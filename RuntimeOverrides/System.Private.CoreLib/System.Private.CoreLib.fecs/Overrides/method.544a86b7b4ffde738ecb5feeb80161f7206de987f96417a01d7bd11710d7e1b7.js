import { primitiveEquals } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Double.Equals(System.Object)'] = function (ctx, $this, other) {
    return primitiveEquals(ctx, $this, other);
};
