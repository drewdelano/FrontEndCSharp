import { initializeLazy } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-lazy.js';
globalThis.DotNet.Methods['System.Lazy`1[T]..ctor(System.Func`1[TResult])'] = function (ctx, lazy, valueFactory) {
    initializeLazy(ctx, lazy, valueFactory);
};
