import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.MobX.ObservableBox`1[T].set_Value(T)'] = function (ctx, box, value) {
    unwrap(box).set(value === null ? null : unwrap(value));
};
