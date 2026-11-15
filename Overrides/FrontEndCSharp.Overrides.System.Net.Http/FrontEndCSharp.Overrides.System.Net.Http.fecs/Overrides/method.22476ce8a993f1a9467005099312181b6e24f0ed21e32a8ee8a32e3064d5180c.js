import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['System.Net.Http.HttpRequestMessage.CheckDisposed()'] = function (ctx, $this) {
    if (unwrap(unwrap($this)._disposed)) {
        throwManagedException(
            ctx,
            'System.ObjectDisposedException',
            'Cannot access a disposed HttpRequestMessage.');
    }
};
