import { isLazyValueCreated } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-lazy.js';
import { wrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.Lazy`1[T].get_IsValueCreated()'] = function (ctx, lazy) {
    return wrap(isLazyValueCreated(ctx, lazy), 'System.Boolean');
};
