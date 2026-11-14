import { createCompletedTask } from '../runtime-assets/System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task.FromResult<TResult>(TResult)'] = function (ctx, result) {
    return createCompletedTask('System.Threading.Tasks.Task`1[TResult]', result, ctx.genericParams);
};
