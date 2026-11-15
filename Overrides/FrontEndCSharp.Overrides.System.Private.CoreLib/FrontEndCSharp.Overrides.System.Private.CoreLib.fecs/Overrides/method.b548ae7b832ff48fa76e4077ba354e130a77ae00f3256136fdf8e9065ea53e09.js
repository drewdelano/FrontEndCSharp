import { initializeListFromEnumerable } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';

globalThis.DotNet.Methods['System.Collections.Generic.List`1[T]..ctor(System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, list, source) {
    initializeListFromEnumerable(ctx, list, source);
};
