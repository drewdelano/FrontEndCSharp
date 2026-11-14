import { groupAt } from '../runtime-assets/System.Text.RegularExpressions.fecs/managed-regex.js';
globalThis.DotNet.Methods['System.Text.RegularExpressions.GroupCollection.get_Item(System.Int32)'] = function (_ctx, collection, index) {
    return groupAt(collection, index);
};
