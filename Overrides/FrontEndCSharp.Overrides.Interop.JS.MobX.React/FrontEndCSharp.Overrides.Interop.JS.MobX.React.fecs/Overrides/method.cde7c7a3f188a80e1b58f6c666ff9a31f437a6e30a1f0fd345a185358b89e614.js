import { Observer } from 'mobx-react-lite';
import { createElement } from 'react';
import { unwrap, wrap } from '../msil-type-helpers.js';
import { invokeDelegate } from '../jit-engine/calls.js';

globalThis.DotNet.Methods['Interop.JS.MobX.React.MobXReact.Observe(System.Func`1[TResult])'] = function (ctx, render) {
    return wrap(
        createElement(Observer, {
            render: () => unwrap(invokeDelegate(ctx, render, []))
        }),
        'Interop.JS.React.ReactElement');
};
