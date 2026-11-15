import { getNonGenericBuilderValueTask } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.get_Task()'] = function (_ctx, builder) {
    return getNonGenericBuilderValueTask(builder);
};
