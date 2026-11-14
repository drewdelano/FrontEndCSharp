import { throwManagedException } from '../managed-exceptions.js';
import { initializeObject } from '../msil.js';

globalThis.DotNet.Methods['System.Threading.CancellationTokenSource.get_Token()'] = function (ctx, $this) {
    if ($this.v.$fecsDisposed === true) {
        throwManagedException(
            ctx,
            'System.ObjectDisposedException',
            'The CancellationTokenSource has been disposed.');
    }
    const token = initializeObject('System.Threading.CancellationToken');
    token.v.$fecsCancellationSource = $this;
    return token;
};
