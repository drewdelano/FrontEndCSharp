import { throwManagedException } from '../managed-exceptions.js';
import { validateValue } from './method.d1a7884ad63b8d433d6bb63dcb84c4923e170e7a93bac3f4fce39fc6db12d7ac.js';

globalThis.DotNet.Methods['System.ComponentModel.DataAnnotations.Validator.ValidateValue(System.Object, System.ComponentModel.DataAnnotations.ValidationContext, System.Collections.Generic.IEnumerable`1[T])'] = function (ctx, value, validationContext, validationAttributes) {
    if (!validateValue(ctx, value, validationContext, null, validationAttributes)) {
        throwManagedException(ctx, 'System.ComponentModel.DataAnnotations.ValidationException', 'The value is invalid.');
    }
};
