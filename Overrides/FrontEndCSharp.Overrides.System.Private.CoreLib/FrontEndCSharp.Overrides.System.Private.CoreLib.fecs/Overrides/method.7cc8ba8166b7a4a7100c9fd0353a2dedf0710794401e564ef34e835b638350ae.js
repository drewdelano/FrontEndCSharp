import { stringGetEnumerator } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-char-enumerator.js';

globalThis.DotNet.Methods['System.String.GetEnumerator()'] = function (_ctx, value) {
    return stringGetEnumerator(value);
};
