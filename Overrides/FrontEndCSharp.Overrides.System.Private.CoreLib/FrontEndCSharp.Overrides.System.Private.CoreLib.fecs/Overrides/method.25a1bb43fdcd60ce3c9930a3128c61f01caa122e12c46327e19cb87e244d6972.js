import { unwrap } from '../msil-type-helpers.js';
import { setTaskExceptionOrCancellation } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)'] = function (ctx, $this, exception) {
    const taskMethodBuilder = unwrap($this);
    setTaskExceptionOrCancellation(taskMethodBuilder.completionSource, exception);
};
