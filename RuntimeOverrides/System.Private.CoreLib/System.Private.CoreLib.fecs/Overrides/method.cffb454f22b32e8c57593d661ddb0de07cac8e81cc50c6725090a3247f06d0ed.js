import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Array.Copy(System.Array, System.Int32, System.Array, System.Int32, System.Int32)'] = function (_ctx, sourceArray, sourceIndex, destinationArray, destinationIndex, length) {
    const source = unwrap(sourceArray);
    const destination = unwrap(destinationArray);
    const sourceOffset = Number(unwrap(sourceIndex));
    const destinationOffset = Number(unwrap(destinationIndex));
    const count = Number(unwrap(length));
    const copy = source.slice(sourceOffset, sourceOffset + count);
    for (let index = 0; index < count; index++) destination[destinationOffset + index] = copy[index];
};
