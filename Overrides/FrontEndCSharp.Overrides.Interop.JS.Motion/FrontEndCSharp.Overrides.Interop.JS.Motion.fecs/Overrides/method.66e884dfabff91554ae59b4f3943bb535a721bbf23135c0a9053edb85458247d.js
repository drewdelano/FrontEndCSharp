import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Motion.MotionTransition.SetDelay(System.Double)'] = function (ctx, transition, seconds) {
    unwrap(transition).delay = unwrap(seconds);
    return transition;
};
