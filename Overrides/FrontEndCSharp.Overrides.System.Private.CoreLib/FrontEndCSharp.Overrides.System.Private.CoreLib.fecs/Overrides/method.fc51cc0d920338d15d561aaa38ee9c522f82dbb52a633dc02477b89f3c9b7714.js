import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String.Format(System.String, System.Object, System.Object, System.Object)'] = function (_ctx, format, first, second, third) {
    const values = [first, second, third].map(value => unwrap(value));
    const result = unwrap(format)
        .replace(/\{\{/g, '\u0000')
        .replace(/\}\}/g, '\u0001')
        .replace(/\{([012])(?:,[^}:]+)?(?::[^}]+)?\}/g, (_match, index) => values[index] == null ? '' : String(values[index]))
        .replace(/\u0000/g, '{')
        .replace(/\u0001/g, '}');
    return wrap(result, 'System.String');
};
