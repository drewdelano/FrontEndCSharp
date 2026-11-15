import { hashSetContains } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-hash-set.js';
globalThis.DotNet.Methods['System.Collections.Generic.HashSet`1[T].Contains(T)'] = function (_ctx, set, value) {
    return hashSetContains(set, value);
};
