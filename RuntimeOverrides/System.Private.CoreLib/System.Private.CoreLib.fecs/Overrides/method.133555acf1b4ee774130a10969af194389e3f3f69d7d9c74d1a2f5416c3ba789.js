import { managedPrimitiveEquals } from '../runtime-assets/System.Private.CoreLib.fecs/managed-object.js';
import { unwrap, wrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.ValueTuple`5[T1, T2, T3, T4, T5].Equals(System.ValueTuple`5[T1, T2, T3, T4, T5])'] = function (_ctx, left, right) {
    const leftValue = unwrap(left);
    const rightValue = unwrap(right);
    const equal = ['Item1', 'Item2', 'Item3', 'Item4', 'Item5'].every(name => {
        const primitive = managedPrimitiveEquals(leftValue[name], rightValue[name]);
        return primitive ?? unwrap(leftValue[name]) === unwrap(rightValue[name]);
    });
    return wrap(equal, 'System.Boolean');
};
