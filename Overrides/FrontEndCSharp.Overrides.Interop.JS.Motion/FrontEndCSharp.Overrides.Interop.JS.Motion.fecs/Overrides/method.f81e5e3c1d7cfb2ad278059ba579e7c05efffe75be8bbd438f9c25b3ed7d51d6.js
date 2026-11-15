import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Motion.MotionStyle.SetString(System.String, System.String)'] = function (ctx, style, name, value) {
    unwrap(style)[unwrap(name)] = unwrap(value);
    return style;
};
