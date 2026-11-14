import { matchGroups } from '../runtime-assets/System.Text.RegularExpressions.fecs/managed-regex.js';
globalThis.DotNet.Methods['System.Text.RegularExpressions.Match.get_Groups()'] = function (_ctx, match) {
    return matchGroups(match);
};
