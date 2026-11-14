import { throwManagedException } from '../managed-exceptions.js';
import { unwrap } from '../msil-type-helpers.js';
import { validateObject } from './method.d1a7884ad63b8d433d6bb63dcb84c4923e170e7a93bac3f4fce39fc6db12d7ac.js';

globalThis.DotNet.Methods['System.ComponentModel.DataAnnotations.Validator.ValidateObject(System.Object, System.ComponentModel.DataAnnotations.ValidationContext, System.Boolean)'] = function (ctx, instance, validationContext, validateAllProperties) {
    if (!validateObject(ctx, instance, validationContext, null, Boolean(unwrap(validateAllProperties)))) {
        throwManagedException(ctx, 'System.ComponentModel.DataAnnotations.ValidationException', 'The object is invalid.');
    }
};
