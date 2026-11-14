import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Array.Copy(System.Array, System.Array, System.Int32)'] = function (ctx, sourceArray, destinationArray, length) {
    const source = unwrap(sourceArray);
    const destination = unwrap(destinationArray);
    const count = Number(unwrap(length));
    const copy = source.slice(0, count);
    for (let index = 0; index < count; index++) destination[index] = copy[index];
};
