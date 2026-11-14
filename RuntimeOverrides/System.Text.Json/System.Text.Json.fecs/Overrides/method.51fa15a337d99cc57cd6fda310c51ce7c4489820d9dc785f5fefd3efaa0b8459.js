import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Text.Json.JsonSerializerOptions.VerifyMutable()'] = function (ctx, $this) {
    const value = unwrap(unwrap($this)._isReadOnly);
    if (value === true || value === 1) {
        throwManagedException(ctx, 'System.InvalidOperationException', 'This JsonSerializerOptions instance is read-only or has already been used in serialization or deserialization.');
    }
};
