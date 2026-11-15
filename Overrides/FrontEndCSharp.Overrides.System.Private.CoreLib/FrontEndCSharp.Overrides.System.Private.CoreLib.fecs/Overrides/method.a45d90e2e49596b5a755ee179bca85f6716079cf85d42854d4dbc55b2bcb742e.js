import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String..ctor(System.ReadOnlySpan`1[T])'] = function (_ctx, value, characters) {
    const span = unwrap(characters);
    value.v = Array.isArray(span)
        ? span.map(character => String.fromCharCode(Number(unwrap(character)))).join('')
        : String(span ?? '');
};
