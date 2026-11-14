import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String.Join(System.String, System.Object[])'] = function (_ctx, separator, values) {
    const delimiter = unwrap(separator) ?? '';
    const items = unwrap(values);
    return wrap(items.map(item => {
        const value = unwrap(item);
        return value == null ? '' : String(value);
    }).join(delimiter), 'System.String');
};
