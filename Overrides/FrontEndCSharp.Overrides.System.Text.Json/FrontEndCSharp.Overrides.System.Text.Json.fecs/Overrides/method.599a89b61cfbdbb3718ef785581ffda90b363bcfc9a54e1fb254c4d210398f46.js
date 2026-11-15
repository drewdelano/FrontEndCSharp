import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

function scalar(value) {
    while (value !== null && typeof value === 'object' && 't' in value && 'v' in value) {
        value = unwrap(value);
    }
    return value;
}

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions.set_IgnoreNullValues(System.Boolean)'] = function (ctx, $this, value) {
    const options = unwrap($this);
    const isReadOnly = scalar(options._isReadOnly);
    if (isReadOnly === true || isReadOnly === 1) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.');
    }

    const ignoreNullValuesValue = scalar(value);
    const ignoreNullValues = ignoreNullValuesValue === true || ignoreNullValuesValue === 1;
    const defaultIgnoreCondition = scalar(options._defaultIgnoreCondition);
    if (ignoreNullValues && defaultIgnoreCondition !== undefined && defaultIgnoreCondition !== 0) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'DefaultIgnoreCondition and IgnoreNullValues cannot both be set.');
    }
    options._ignoreNullValues = wrap(ignoreNullValues, 'System.Boolean');
};
