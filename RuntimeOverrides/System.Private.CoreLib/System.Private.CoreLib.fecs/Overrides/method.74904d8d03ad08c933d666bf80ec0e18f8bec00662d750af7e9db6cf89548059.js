import { getNonGenericBuilderValueTask } from '../runtime-assets/System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.get_Task()'] = function (_ctx, builder) {
    return getNonGenericBuilderValueTask(builder);
};
