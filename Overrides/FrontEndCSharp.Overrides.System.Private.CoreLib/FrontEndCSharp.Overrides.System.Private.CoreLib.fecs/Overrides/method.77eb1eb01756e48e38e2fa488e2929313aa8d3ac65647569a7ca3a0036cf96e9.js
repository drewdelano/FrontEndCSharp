import { createCompletedTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-completion-source.js';

const completedTask = createCompletedTask('System.Threading.Tasks.Task');

globalThis.DotNet.Methods['System.Threading.Tasks.Task.get_CompletedTask()'] = function () {
    return completedTask;
};
