import { objectEquals } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';

globalThis.DotNet.Methods['System.Object.Equals(System.Object)'] = function (ctx, $this, other) {
    return objectEquals(ctx, $this, other);
};
