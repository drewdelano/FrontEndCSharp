import { runInAction } from 'mobx';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.MobX.MobX.RunInAction(System.Action)'] = function (ctx, action) {
    runInAction(() => invokeDelegate(ctx, action));
};
