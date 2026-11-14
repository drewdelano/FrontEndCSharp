import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.String.Format(System.String, System.Object)'] = function (_ctx, format, argument) {
    const template = unwrap(format);
    const raw = unwrap(argument);
    const value = raw == null ? '' : String(raw);
    const result = template
        .replace(/\{\{/g, '\u0000')
        .replace(/\}\}/g, '\u0001')
        .replace(/\{0(?:,[^}:]+)?(?::[^}]+)?\}/g, value)
        .replace(/\u0000/g, '{')
        .replace(/\u0001/g, '}');
    return wrap(result, 'System.String');
};
