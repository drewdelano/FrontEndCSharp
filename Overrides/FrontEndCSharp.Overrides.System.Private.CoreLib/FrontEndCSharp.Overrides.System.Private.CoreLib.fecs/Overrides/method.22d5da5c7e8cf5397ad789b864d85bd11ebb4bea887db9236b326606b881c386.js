import { runtimeIdentityHash } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-object.js';
import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(System.Object)'] = function (_ctx, value) {
    return wrap(runtimeIdentityHash(value), 'System.Int32');
};
