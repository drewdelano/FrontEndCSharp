import { cancelCancellationSource } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-cancellation.js';
import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.Threading.CancellationTokenSource.Cancel()'] = function (ctx, $this) {
    if ($this.v.$fecsDisposed === true) {
        throwManagedException(
            ctx,
            'System.ObjectDisposedException',
            'The CancellationTokenSource has been disposed.');
    }
    cancelCancellationSource($this);
};
