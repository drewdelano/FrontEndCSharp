import { charEnumeratorCurrent } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-char-enumerator.js';

globalThis.DotNet.Methods['System.CharEnumerator.get_Current()'] = function (_ctx, enumerator) {
    return charEnumeratorCurrent(enumerator);
};
