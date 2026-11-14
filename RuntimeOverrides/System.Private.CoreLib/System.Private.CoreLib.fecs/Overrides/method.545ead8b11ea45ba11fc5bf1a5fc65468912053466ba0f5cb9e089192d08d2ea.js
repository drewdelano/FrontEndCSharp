import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Environment.get_CurrentManagedThreadId()'] = function () {
    return wrap(1, 'System.Int32');
};
