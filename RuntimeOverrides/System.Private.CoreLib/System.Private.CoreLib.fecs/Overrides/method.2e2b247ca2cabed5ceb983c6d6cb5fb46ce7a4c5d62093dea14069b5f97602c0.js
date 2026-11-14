import { isManagedNull, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Delegate.Combine(System.Delegate, System.Delegate)'] = function (_ctx, left, right) {
    if (isManagedNull(left)) return right;
    if (isManagedNull(right)) return left;
    if (left.t !== right.t) throw new TypeError('Delegates must be of the same type.');
    return wrap(
        { invocations: [...left.v.invocations, ...right.v.invocations] },
        left.t,
        left.$typeArgs ?? null);
};
