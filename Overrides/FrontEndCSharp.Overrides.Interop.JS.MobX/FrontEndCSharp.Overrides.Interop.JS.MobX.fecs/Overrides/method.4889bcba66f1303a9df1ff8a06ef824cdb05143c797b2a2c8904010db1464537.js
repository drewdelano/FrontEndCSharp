import { unwrap } from '../msil-type-helpers.js';

globalThis.DotNet.Methods['Interop.JS.MobX.Reaction.Dispose()'] = function (ctx, reaction) {
    unwrap(reaction)();
};
