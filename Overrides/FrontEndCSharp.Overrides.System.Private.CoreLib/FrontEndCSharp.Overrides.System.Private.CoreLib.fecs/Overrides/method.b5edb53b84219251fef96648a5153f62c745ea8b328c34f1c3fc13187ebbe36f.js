import { wrap } from '../msil-type-helpers.js';
globalThis.DotNet.Methods['System.StringComparer.get_OrdinalIgnoreCase()'] = function () {
    return wrap({}, 'System.OrdinalIgnoreCaseComparer');
};
