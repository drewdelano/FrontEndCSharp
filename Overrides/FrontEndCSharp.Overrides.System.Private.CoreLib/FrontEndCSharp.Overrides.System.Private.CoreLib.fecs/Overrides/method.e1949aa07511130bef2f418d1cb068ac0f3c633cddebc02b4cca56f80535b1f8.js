import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Array.Resize<T>(T[]&, System.Int32)'] = function (_ctx, arrayReference, newSize) {
    const original = arrayReference.value;
    const requestedSize = Number(unwrap(newSize));
    const resized = unwrap(original).slice(0, requestedSize);
    while (resized.length < requestedSize) resized.push(null);
    arrayReference.value = wrap(resized, original.t, original.$typeArgs ?? null);
};
