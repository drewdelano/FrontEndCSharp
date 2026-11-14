import { observable } from 'mobx';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.MobX.MobX.Box<T>(T)'] = function (ctx, initialValue) {
    const genericTypeMap = ctx.genericParams === undefined ? null : { ...ctx.genericParams };
    return wrap(
        observable.box(initialValue === null ? null : unwrap(initialValue)),
        'Interop.JS.MobX.ObservableBox`1[T]',
        genericTypeMap);
};
