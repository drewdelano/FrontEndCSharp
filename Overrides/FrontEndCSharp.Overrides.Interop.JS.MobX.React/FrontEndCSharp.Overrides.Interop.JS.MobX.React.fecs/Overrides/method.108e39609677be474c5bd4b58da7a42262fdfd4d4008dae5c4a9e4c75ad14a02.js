import { observer } from 'mobx-react-lite';
import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.MobX.React.MobXReact.Observer<TProps>(Interop.JS.React.ReactComponent`1[TProps])'] = function (ctx, component) {
    const genericTypeMap = ctx.genericParams === undefined ? null : { ...ctx.genericParams };
    return wrap(
        observer(unwrap(component)),
        'Interop.JS.React.ReactComponent`1[TProps]',
        genericTypeMap);
};
