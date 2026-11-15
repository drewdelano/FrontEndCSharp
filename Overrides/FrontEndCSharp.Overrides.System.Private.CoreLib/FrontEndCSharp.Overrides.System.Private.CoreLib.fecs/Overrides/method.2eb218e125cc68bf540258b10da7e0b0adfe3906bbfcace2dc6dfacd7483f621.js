import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Exception.get_StackTrace()"] = function (ctx, thisArg) {
    const ex = unwrap(thisArg);
    return ex._stackTraceString;
};
