import { unwrap } from '../msil-type-helpers.js';

const listenerStoreKey = Symbol.for('Interop.JS.Browser.eventListeners');
const listenerStore = globalThis[listenerStoreKey] ??= new WeakMap();

globalThis.DotNet.Methods['Interop.JS.Browser.Element.RemoveEventListener(System.String, System.Action`1[T])'] = function (ctx, element, type, listener) {
    const nativeElement = unwrap(element);
    const nativeType = unwrap(type);
    const elementListeners = listenerStore.get(listener);
    const typeListeners = elementListeners?.get(nativeElement);
    const nativeListener = typeListeners?.get(nativeType);
    if (nativeListener === undefined) {
        return;
    }
    nativeElement.removeEventListener(nativeType, nativeListener);
    typeListeners.delete(nativeType);
};
