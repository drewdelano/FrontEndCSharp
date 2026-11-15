import { startAsyncValueTaskMethodBuilder } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/value-task.js';
globalThis.DotNet.Methods['System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.Start<TStateMachine>(TStateMachine&)'] = function (ctx, _builder, stateMachine) {
    startAsyncValueTaskMethodBuilder(ctx, stateMachine);
};
