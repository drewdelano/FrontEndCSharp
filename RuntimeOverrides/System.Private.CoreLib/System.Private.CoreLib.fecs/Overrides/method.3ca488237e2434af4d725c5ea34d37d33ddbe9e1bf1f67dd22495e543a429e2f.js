import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String.StartsWith(System.String, System.StringComparison)'] = function (ctx, thisArg, value, comparisonType) {
    const source = unwrap(thisArg);
    const prefix = unwrap(value);
    const comparison = Number(unwrap(comparisonType));
    if (source === null || prefix === null) throw new TypeError('String value cannot be null.');
    let matches;
    if (comparison === 1 || comparison === 3 || comparison === 5) {
        matches = source.substring(0, prefix.length).toLocaleLowerCase()
            === prefix.toLocaleLowerCase();
    } else {
        matches = source.startsWith(prefix);
    }
    return wrap(matches, 'System.Boolean');
};
