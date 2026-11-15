import { configure } from 'mobx';
import { unwrap } from '../msil-type-helpers.js';

const actionEnforcementNames = ['observed', 'never', 'always'];

globalThis.DotNet.Methods['Interop.JS.MobX.MobX.Configure(Interop.JS.MobX.MobXActionEnforcement)'] = function (_ctx, actionEnforcement) {
    const value = unwrap(actionEnforcement);
    const name = actionEnforcementNames[value];
    if (name === undefined) {
        throw new RangeError(`Unknown MobX action-enforcement value '${value}'.`);
    }
    configure({ enforceActions: name });
};
