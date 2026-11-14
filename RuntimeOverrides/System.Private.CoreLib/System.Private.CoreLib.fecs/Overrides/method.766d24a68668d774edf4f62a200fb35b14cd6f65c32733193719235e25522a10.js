import { charEnumeratorDispose } from '../runtime-assets/System.Private.CoreLib.fecs/managed-char-enumerator.js';

globalThis.DotNet.Methods['System.CharEnumerator.Dispose()'] = function () {
    charEnumeratorDispose();
};
