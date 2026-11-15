import { createFaultedTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';
import { requireNonNullTaskArgument } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-argument-validation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.FromException<TResult>(System.Exception)'] = function (ctx, exception) {
    requireNonNullTaskArgument(ctx, exception, 'exception');
    return createFaultedTask('System.Threading.Tasks.Task`1[TResult]', exception, ctx.genericParams);
};
