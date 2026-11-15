import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Motion.MotionTransition.SetType(System.String)'] = function (ctx, transition, type) {
    unwrap(transition).type = unwrap(type);
    return transition;
};
