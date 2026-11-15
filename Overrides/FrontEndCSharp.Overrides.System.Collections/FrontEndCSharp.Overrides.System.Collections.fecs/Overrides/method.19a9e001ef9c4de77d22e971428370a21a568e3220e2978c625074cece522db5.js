import { initializeStack } from '../runtime-assets/FrontEndCSharp.Overrides.System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1[T]..ctor()'] = function (_ctx, stack) {
    initializeStack(stack);
};
