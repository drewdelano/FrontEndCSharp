import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Motion.MotionStyle.SetNumber(System.String, System.Double)'] = function (ctx, style, name, value) {
    unwrap(style)[unwrap(name)] = unwrap(value);
    return style;
};
