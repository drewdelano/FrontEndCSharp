import { getManagedTypeName, unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.MobX.ObservableBox`1[T].get_Value()'] = function (ctx, box) {
    const value = unwrap(box).get();
    if (value === null) {
        return null;
    }
    const typeReference = box.$typeArgs?.T ?? ctx.genericParams?.T;
    if (typeReference === null || typeReference === undefined) {
        throw new TypeError('The closed managed type for ObservableBox<T> is unavailable.');
    }
    getManagedTypeName(typeReference);
    return wrap(value, typeReference);
};
