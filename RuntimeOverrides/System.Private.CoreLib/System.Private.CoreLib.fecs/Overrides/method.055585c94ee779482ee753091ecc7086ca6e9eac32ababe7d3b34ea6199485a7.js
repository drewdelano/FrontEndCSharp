import { deRef, unwrap } from '../msil-type-helpers.js';
import { registerAwaiterContinuation } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.TaskAwaiter`1[TResult].OnCompleted(System.Action)'] = function (ctx, $this, continuation) {
    registerAwaiterContinuation(deRef($this), unwrap(continuation));
};
