import { deRef, unwrap } from '../msil-type-helpers.js';
import { registerAwaiterContinuation } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[TResult].UnsafeOnCompleted(System.Action)'] = function (ctx, $this, continuation) {
    registerAwaiterContinuation(deRef($this), unwrap(continuation));
};
