import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String.Split(System.Char[])'] = function (ctx, thisArg, separator) {
    const source = unwrap(thisArg);
    const separatorValues = unwrap(separator) ?? [];
    const separators = separatorValues.map(value => unwrap(value));
    const pattern = separators.length === 0
        ? /\s/
        : new RegExp(`[${separators.map(value =>
            String(value).replace(/[\\\]\[\-^]/g, '\\$&')).join('')}]`);
    return wrap(
        source.split(pattern).map(value => wrap(value, 'System.String')),
        'System.String[]');
};
