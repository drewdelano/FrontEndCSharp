import { getDateTimeOffsetUtcNow } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';

globalThis.DotNet.Methods['System.DateTimeOffset.get_UtcNow()'] = function () {
    return getDateTimeOffsetUtcNow();
};
