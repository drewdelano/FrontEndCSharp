import { listEnumeratorMoveNext } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1+Enumerator[T].MoveNext()'] = function (ctx, $this) {
    return listEnumeratorMoveNext(ctx, $this);
};
