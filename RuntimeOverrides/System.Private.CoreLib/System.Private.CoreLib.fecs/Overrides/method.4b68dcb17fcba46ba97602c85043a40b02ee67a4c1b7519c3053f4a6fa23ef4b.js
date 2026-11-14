import { getDateTimeOffsetLocalDateTime } from '../runtime-assets/System.Private.CoreLib.fecs/managed-temporal.js';
import { throwManagedException } from '../managed-exceptions.js';

globalThis.DotNet.Methods['System.DateTimeOffset.get_LocalDateTime()'] = function (ctx, $this) {
    const value = getDateTimeOffsetLocalDateTime($this);
    if (value === null) throwManagedException(ctx, 'System.ArgumentOutOfRangeException', 'The local DateTime is outside its valid range.');
    return value;
};
