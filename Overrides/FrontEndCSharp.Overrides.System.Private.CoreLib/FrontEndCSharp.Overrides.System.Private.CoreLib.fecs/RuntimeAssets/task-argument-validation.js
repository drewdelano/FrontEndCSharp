import { throwManagedException } from '../../managed-exceptions.js';
import { unwrap, wrap } from '../../msil-type-helpers.js';

export function requireNonNullTaskArgument(ctx, value, parameterName) {
    if (unwrap(value) !== null) {
        return;
    }

    throwManagedException(
        ctx,
        'System.ArgumentNullException',
        `Value cannot be null. (Parameter '${parameterName}')`,
        null,
        { _paramName: wrap(parameterName, 'System.String') });
}

export function requireTaskCollectionArgument(ctx, value, allowEmpty) {
    requireNonNullTaskArgument(ctx, value, 'tasks');
    const tasks = unwrap(value);
    if (!Array.isArray(tasks)) {
        throw new TypeError('A materialized Task collection must be a JavaScript array.');
    }
    if (tasks.some(task => unwrap(task) === null)) {
        throwArgumentException(ctx, 'The tasks argument included a null value.', 'tasks');
    }
    if (!allowEmpty && tasks.length === 0) {
        throwArgumentException(ctx, 'The tasks argument contains no tasks.', 'tasks');
    }
    return tasks;
}

function throwArgumentException(ctx, message, parameterName) {
    throwManagedException(
        ctx,
        'System.ArgumentException',
        `${message} (Parameter '${parameterName}')`,
        null,
        { _paramName: wrap(parameterName, 'System.String') });
}
