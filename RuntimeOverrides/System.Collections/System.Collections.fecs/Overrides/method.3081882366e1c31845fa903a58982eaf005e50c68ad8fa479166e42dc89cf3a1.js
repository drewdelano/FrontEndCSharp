import { stackPush } from '../runtime-assets/System.Collections.fecs/managed-stack.js';
globalThis.DotNet.Methods['System.Collections.Generic.Stack`1[T].Push(T)'] = function (ctx, stack, value) {
    stackPush(ctx, stack, value);
};
