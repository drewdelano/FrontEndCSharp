import { createNonGenericAsyncValueTaskMethodBuilder } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Create()'] = function () {
    return createNonGenericAsyncValueTaskMethodBuilder();
};
