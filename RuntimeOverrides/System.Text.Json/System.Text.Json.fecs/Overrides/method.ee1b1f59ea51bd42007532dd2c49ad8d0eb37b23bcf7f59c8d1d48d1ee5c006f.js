import { throwManagedException } from '../managed-exceptions.js';
import { unwrap, wrap } from '../msil-type-helpers.js';

function scalar(value) {
    while (value !== null && typeof value === 'object' && 't' in value && 'v' in value) {
        value = unwrap(value);
    }
    return value;
}

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions.set_ReadCommentHandling(System.Text.Json.JsonCommentHandling)'] = function (ctx, $this, value) {
    const options = unwrap($this);
    const isReadOnly = scalar(options._isReadOnly);
    if (isReadOnly === true || isReadOnly === 1) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.');
    }

    const handling = scalar(value);
    if (handling !== 0 && handling !== 1) {
        throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'Comments cannot be stored in the JSON object graph. (Parameter \'value\')');
    }
    options._readCommentHandling = wrap(handling, 'System.Text.Json.JsonCommentHandling');
};
