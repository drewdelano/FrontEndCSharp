import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

function scalar(value) {
    while (value !== null && typeof value === 'object' && 't' in value && 'v' in value) {
        value = unwrap(value);
    }
    return value;
}

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions.set_DefaultIgnoreCondition(System.Text.Json.Serialization.JsonIgnoreCondition)'] = function (ctx, $this, value) {
    const options = unwrap($this);
    const isReadOnly = scalar(options._isReadOnly);
    if (isReadOnly === true || isReadOnly === 1) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.');
    }

    const condition = scalar(value);
    if (condition === 1) {
        throwManagedException(ctx, 'System.ArgumentException', 'The value cannot be JsonIgnoreCondition.Always.');
    }
    const ignoreNullValues = scalar(options._ignoreNullValues);
    if (condition !== 0 && (ignoreNullValues === true || ignoreNullValues === 1)) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'DefaultIgnoreCondition and IgnoreNullValues cannot both be set.');
    }
    options._defaultIgnoreCondition = wrap(
        condition,
        'System.Text.Json.Serialization.JsonIgnoreCondition');
};
