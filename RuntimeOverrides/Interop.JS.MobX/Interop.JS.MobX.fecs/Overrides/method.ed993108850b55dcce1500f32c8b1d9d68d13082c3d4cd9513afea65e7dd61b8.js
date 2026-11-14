import { makeObservable, observable } from 'mobx';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.MobX.MobX.MakeAutoObservable<T>(T)'] = function (ctx, target) {
    const value = unwrap(target);
    const annotations = {};
    for (const name of Reflect.ownKeys(value)) {
        const descriptor = Object.getOwnPropertyDescriptor(value, name);
        if (descriptor?.configurable === true && Object.hasOwn(descriptor, 'value')) {
            // Observe compiler-owned storage fields. Non-configurable readable
            // aliases preserve CLR field identity and must not be replaced by a
            // JavaScript library.
            annotations[name] = observable;
        }
    }
    makeObservable(value, annotations);
    return target;
};
