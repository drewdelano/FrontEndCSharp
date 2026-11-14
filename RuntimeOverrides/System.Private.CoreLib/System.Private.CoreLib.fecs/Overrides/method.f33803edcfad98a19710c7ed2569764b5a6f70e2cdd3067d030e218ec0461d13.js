import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Threading.CancellationToken.ThrowOperationCanceledException()'] = function (ctx) {
    throwManagedException(ctx, 'System.OperationCanceledException');
};
