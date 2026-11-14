import { createElement } from 'react';
import { motion } from 'motion/react';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { materializeReactProps } from '../runtime-assets/Interop.JS.React.fecs/react-props.js';

const motionElements = new Map();

globalThis.DotNet.Methods['Interop.JS.Motion.Motion.Element(System.String, Interop.JS.Motion.MotionProps, Interop.JS.React.ReactNode[])'] = function (ctx, elementName, props, children) {
    const name = unwrap(elementName);
    let motionElement = motionElements.get(name);
    if (motionElement === undefined) {
        motionElement = motion.create(name);
        motionElements.set(name, motionElement);
    }
    const nativeChildren = unwrap(children).map(child => unwrap(child));
    const nativeProps = materializeReactProps(ctx, props, [
        'Interop.JS.Motion.MotionStyle',
        'Interop.JS.Motion.MotionTransition'
    ]);
    return wrap(
        createElement(motionElement, nativeProps, ...nativeChildren),
        'Interop.JS.React.ReactElement');
};
