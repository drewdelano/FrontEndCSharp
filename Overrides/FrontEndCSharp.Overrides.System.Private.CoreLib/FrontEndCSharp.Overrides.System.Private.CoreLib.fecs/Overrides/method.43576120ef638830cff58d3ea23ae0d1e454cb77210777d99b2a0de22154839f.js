import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Char.IsUpper(System.Char)'] = function (_ctx, value) {
    const character = String.fromCharCode(Number(unwrap(value)));
    return wrap(character.toUpperCase() === character && character.toLowerCase() !== character, 'System.Boolean');
};
