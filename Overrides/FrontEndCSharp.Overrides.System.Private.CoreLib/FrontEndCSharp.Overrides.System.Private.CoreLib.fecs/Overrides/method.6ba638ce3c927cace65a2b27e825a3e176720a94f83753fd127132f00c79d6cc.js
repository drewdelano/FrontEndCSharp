import { wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Collections.Generic.EqualityComparer`1[T].get_Default()'] = function (ctx) {
    return wrap({}, 'System.Collections.Generic.IEqualityComparer`1[T]', ctx.genericParams);
};
