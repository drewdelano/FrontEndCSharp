import { wrap, unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods["System.Exception.ToString()"] = function (ctx, thisArg) {
    const ex = unwrap(thisArg);
    const toStringed = `${thisArg.t}: ${ex._message}\n\n${ex._stackTraceString}`;

    // TODO: this doesn't pull in inner exceptions like it should
    return toStringed;
};
