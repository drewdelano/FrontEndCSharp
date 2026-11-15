import { unwrap, wrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.Browser.DomEvent.get_CurrentTarget()'] = function (ctx, domEvent) {
    const currentTarget = unwrap(domEvent).currentTarget;
    return currentTarget === null
        ? null
        : wrap(currentTarget, 'Interop.JS.Browser.Element');
};
