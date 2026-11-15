import { defaultTaskScheduler } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/task-continuation.js';

globalThis.DotNet.Methods['System.Threading.Tasks.TaskScheduler.get_Current()'] = function () {
    return defaultTaskScheduler;
};
