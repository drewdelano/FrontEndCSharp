import { listEnumeratorReset } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1+Enumerator[T].Reset()'] = function (ctx, $this) {
    listEnumeratorReset(ctx, $this);
};
