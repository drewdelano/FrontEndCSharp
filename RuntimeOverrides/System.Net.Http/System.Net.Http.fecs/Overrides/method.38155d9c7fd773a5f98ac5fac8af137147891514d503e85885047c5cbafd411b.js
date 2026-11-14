import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpResponseMessage..ctor(System.Net.HttpStatusCode)'] = function (ctx, $this, statusCode) {
    const numericStatusCode = unwrap(statusCode);
    if (numericStatusCode < 0 || numericStatusCode > 999) {
        throwManagedException(
            ctx,
            'System.ArgumentOutOfRangeException',
            'The HTTP status code must be between 0 and 999.');
    }

    unwrap($this)._statusCode = statusCode;
};
