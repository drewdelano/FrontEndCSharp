import { stringGetEnumerator } from '../runtime-assets/System.Private.CoreLib.fecs/managed-char-enumerator.js';

globalThis.DotNet.Methods['System.Collections.IEnumerable.GetEnumerator()'] = function (_ctx, value) {
    if (value?.t !== 'System.String') {
        throw new TypeError(`The non-generic IEnumerable fallback does not support '${value?.t ?? 'unknown'}'.`);
    }
    return stringGetEnumerator(value);
};
