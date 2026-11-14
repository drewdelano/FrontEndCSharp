import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Threading.Interlocked.CompareExchange<T>(T&, T, T)'] = function (ctx, location, value, comparand) {
    const original = location.value;
    if (unwrap(original) === unwrap(comparand)) location.value = value;
    return original;
};
