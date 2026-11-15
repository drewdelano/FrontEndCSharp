import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

function scalar(value) {
    while (value !== null && typeof value === 'object' && 't' in value && 'v' in value) {
        value = unwrap(value);
    }
    return value;
}

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions.set_MaxDepth(System.Int32)'] = function (ctx, $this, value) {
    const options = unwrap($this);
    const isReadOnly = scalar(options._isReadOnly);
    if (isReadOnly === true || isReadOnly === 1) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.');
    }

    const maxDepth = scalar(value);
    if (!Number.isInteger(maxDepth) || maxDepth < 0) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Max depth must be positive. (Parameter \'value\')');
    }
    options._maxDepth = wrap(maxDepth, 'System.Int32');
    options['<EffectiveMaxDepth>k__BackingField'] = wrap(
        maxDepth === 0 ? 64 : maxDepth,
        'System.Int32');
};
