import { unwrap, wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

const listenerStoreKey = Symbol.for('Interop.JS.Browser.eventListeners');
const listenerStore = globalThis[listenerStoreKey] ??= new WeakMap();

globalThis.DotNet.Methods['Interop.JS.Browser.Element.AddEventListener(System.String, System.Action`1[T])'] = function (ctx, element, type, listener) {
    const nativeElement = unwrap(element);
    const nativeType = unwrap(type);
    let elementListeners = listenerStore.get(listener);
    if (elementListeners === undefined) {
        elementListeners = new WeakMap();
        listenerStore.set(listener, elementListeners);
    }
    let typeListeners = elementListeners.get(nativeElement);
    if (typeListeners === undefined) {
        typeListeners = new Map();
        elementListeners.set(nativeElement, typeListeners);
    }
    let nativeListener = typeListeners.get(nativeType);
    if (nativeListener === undefined) {
        nativeListener = event => invokeDelegate(ctx, listener, [wrap(event, 'Interop.JS.Browser.DomEvent')]);
        typeListeners.set(nativeType, nativeListener);
    }
    nativeElement.addEventListener(nativeType, nativeListener);
};
