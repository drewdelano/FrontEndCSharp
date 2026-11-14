import { getDefaultValue, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.ConditionalWeakTable`2[TKey, TValue].TryGetValue(TKey, TValue&)'] = function (ctx, _table, _key, value) {
    value.value = getDefaultValue('TValue', ctx.genericParams);
    return wrap(false, 'System.Boolean');
};
