import { autorun } from 'mobx';
import { wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.MobX.MobX.Autorun(System.Action)'] = function (ctx, effect) {
    const disposer = autorun(() => invokeDelegate(ctx, effect));
    return wrap(disposer, 'Interop.JS.MobX.Reaction');
};
