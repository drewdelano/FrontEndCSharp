import { getTaskResult } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

globalThis.DotNet.Methods['System.Threading.Tasks.Task`1[TResult].get_Result()'] = function (ctx, $this) {
    return getTaskResult(ctx, $this);
};
