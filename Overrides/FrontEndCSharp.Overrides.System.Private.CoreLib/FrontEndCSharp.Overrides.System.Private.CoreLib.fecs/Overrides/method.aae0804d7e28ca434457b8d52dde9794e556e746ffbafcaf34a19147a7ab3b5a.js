import { charEnumeratorMoveNext } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-char-enumerator.js';

globalThis.DotNet.Methods['System.CharEnumerator.MoveNext()'] = function (_ctx, enumerator) {
    return charEnumeratorMoveNext(enumerator);
};
