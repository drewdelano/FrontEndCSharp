import { charEnumeratorDispose } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-char-enumerator.js';

globalThis.DotNet.Methods['System.CharEnumerator.Dispose()'] = function () {
    charEnumeratorDispose();
};
