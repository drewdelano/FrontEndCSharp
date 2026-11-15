import { wrap } from '../msil-type-helpers.js';
import "./type.System.Runtime.CompilerServices.YieldAwaitable.js";

globalThis.DotNet.Methods['System.Threading.Tasks.Task.Yield()'] = function (ctx) {
    return wrap({}, "System.Runtime.CompilerServices.YieldAwaitable");
};
