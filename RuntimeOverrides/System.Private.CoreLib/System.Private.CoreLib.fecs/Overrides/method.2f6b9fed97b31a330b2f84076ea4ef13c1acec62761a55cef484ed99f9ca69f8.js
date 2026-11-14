import { getDateTimeUtcNow } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTime.get_UtcNow()'] = function () {
    return getDateTimeUtcNow();
};
