import { unwrap, wrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.String.Contains(System.Char)'] = function (_ctx, value, character) {
    return wrap(unwrap(value).includes(String.fromCharCode(Number(unwrap(character)))), 'System.Boolean');
};
