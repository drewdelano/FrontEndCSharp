import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Motion.MotionTransition.SetDuration(System.Double)'] = function (ctx, transition, seconds) {
    unwrap(transition).duration = unwrap(seconds);
    return transition;
};
