import { throwManagedException } from '../managed-exceptions.js';
globalThis.DotNet.Methods['System.Threading.CancellationTokenSource.CreateLinkedTokenSource(System.Threading.CancellationToken, System.Threading.CancellationToken)'] = function (ctx) {
    throwManagedException(
        ctx,
        'System.PlatformNotSupportedException',
        'Linked cancellation tokens are not supported by the browser runtime.');
};
