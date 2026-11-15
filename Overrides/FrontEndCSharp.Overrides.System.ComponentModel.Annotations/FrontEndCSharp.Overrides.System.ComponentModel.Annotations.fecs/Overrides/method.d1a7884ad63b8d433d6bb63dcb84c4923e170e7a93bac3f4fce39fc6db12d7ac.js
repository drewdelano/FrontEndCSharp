import { initializeObject } from '../msil.js';
import { tryGetListItems } from '../runtime-assets/FrontEndCSharp.Overrides.System.Private.CoreLib.fecs/managed-list.js';
import { throwManagedException } from '../managed-exceptions.js';
import { getManagedTypeName, isManagedNull, unwrap, wrap } from '../msil-type-helpers.js';

const validationNamespace = 'System.ComponentModel.DataAnnotations.';

function getBaseTypeName(typeName) {
    const reference = globalThis.DotNet.Types[typeName]?.baseType;
    return reference == null ? null : getManagedTypeName(reference);
}

function typeHierarchy(typeName) {
    const hierarchy = [];
    for (let current = typeName; typeof current === 'string'; current = getBaseTypeName(current)) {
        const metadata = globalThis.DotNet.Types[current];
        if (!metadata) break;
        hierarchy.push(metadata);
    }
    return hierarchy;
}

function named(attribute, name, fallback = null) {
    return Object.prototype.hasOwnProperty.call(attribute.namedArguments ?? {}, name)
        ? attribute.namedArguments[name] : fallback;
}

function displayName(property) {
    if (property === null) return 'value';
    const display = (property.attributes ?? []).find(attribute =>
        attribute.typeName === validationNamespace + 'DisplayAttribute');
    return display === undefined ? property.name : named(display, 'Name', property.name);
}

function format(template, values) {
    for (let index = 0; index < values.length; index++) {
        template = template.replaceAll(`{${index}}`, String(values[index]));
    }
    return template;
}

function explicitMessage(attribute, values) {
    const message = named(attribute, 'ErrorMessage');
    return typeof message === 'string' ? format(message, values) : null;
}

function lengthOf(value) {
    value = unwrap(value);
    return typeof value === 'string' || Array.isArray(value) ? value.length : null;
}

function luhn(value) {
    const digits = String(value).replaceAll('-', '').replaceAll(' ', '');
    if (!/^\d+$/.test(digits)) return false;
    let sum = 0;
    let alternate = false;
    for (let index = digits.length - 1; index >= 0; index--) {
        let digit = Number(digits[index]);
        if (alternate && (digit *= 2) > 9) digit -= 9;
        sum += digit;
        alternate = !alternate;
    }
    return sum % 10 === 0;
}

function findProperty(typeName, propertyName) {
    for (const type of typeHierarchy(typeName)) {
        const property = (type.properties ?? []).find(candidate => candidate.name === propertyName);
        if (property) return property;
    }
    return null;
}

function invokeGetter(ctx, instance, property) {
    const getter = globalThis.DotNet.Methods[property?.getter];
    return typeof getter === 'function' ? getter(ctx, instance) : null;
}

function validateBuiltIn(ctx, attribute, value, property, instance) {
    const scalar = unwrap(value);
    const name = displayName(property);
    const args = attribute.constructorArguments ?? [];
    let valid = true;
    let fallback = null;
    switch (attribute.typeName) {
        case validationNamespace + 'RequiredAttribute': {
            const allowEmpty = Boolean(named(attribute, 'AllowEmptyStrings', false));
            valid = !isManagedNull(value)
                && (allowEmpty || typeof scalar !== 'string' || scalar.trim().length !== 0);
            fallback = `The ${name} field is required.`;
            break;
        }
        case validationNamespace + 'StringLengthAttribute': {
            if (isManagedNull(value)) return null;
            const maximum = Number(args[0]);
            const minimum = Number(named(attribute, 'MinimumLength', 0));
            if (maximum < 0 || minimum < 0 || minimum > maximum) {
                throwManagedException(
                    ctx,
                    'System.InvalidOperationException',
                    'The maximum length must be a nonnegative integer and at least the minimum length.');
            }
            valid = typeof scalar === 'string'
                && scalar.length >= minimum && scalar.length <= maximum;
            fallback = minimum > 0
                ? `The field ${name} must be a string with a minimum length of '${minimum}' and a maximum length of '${maximum}'.`
                : `The field ${name} must be a string with a maximum length of '${maximum}'.`;
            break;
        }
        case validationNamespace + 'MinLengthAttribute': {
            if (isManagedNull(value)) return null;
            const minimum = Number(args[0]);
            const length = lengthOf(value);
            valid = length !== null && length >= minimum;
            fallback = `The field ${name} must be a string or array type with a minimum length of '${minimum}'.`;
            break;
        }
        case validationNamespace + 'MaxLengthAttribute': {
            if (isManagedNull(value)) return null;
            const maximum = Number(args[0]);
            const length = lengthOf(value);
            valid = length !== null && length <= maximum;
            fallback = `The field ${name} must be a string or array type with a maximum length of '${maximum}'.`;
            break;
        }
        case validationNamespace + 'RangeAttribute': {
            if (isManagedNull(value)) return null;
            const minimum = args.length === 2 ? args[0] : args[1];
            const maximum = args.length === 2 ? args[1] : args[2];
            const actual = Number(scalar);
            valid = !Number.isNaN(actual)
                && actual >= Number(minimum) && actual <= Number(maximum);
            fallback = `The field ${name} must be between ${minimum} and ${maximum}.`;
            break;
        }
        case validationNamespace + 'RegularExpressionAttribute': {
            if (isManagedNull(value) || scalar === '') return null;
            const pattern = String(args[0]);
            valid = typeof scalar === 'string' && new RegExp(`^(?:${pattern})$`).test(scalar);
            fallback = `The field ${name} must match the regular expression '${pattern}'.`;
            break;
        }
        case validationNamespace + 'EmailAddressAttribute':
            if (isManagedNull(value)) return null;
            valid = typeof scalar === 'string'
                && scalar.indexOf('@') > 0
                && scalar.indexOf('@') === scalar.lastIndexOf('@')
                && scalar.lastIndexOf('@') < scalar.length - 1;
            fallback = `The ${name} field is not a valid e-mail address.`;
            break;
        case validationNamespace + 'CreditCardAttribute':
            if (isManagedNull(value)) return null;
            valid = luhn(scalar);
            fallback = `The ${name} field is not a valid credit card number.`;
            break;
        case validationNamespace + 'UrlAttribute':
            if (isManagedNull(value)) return null;
            valid = /^(https?|ftp):\/\/[^\s]+$/i.test(String(scalar));
            fallback = `The ${name} field is not a valid fully-qualified http, https, or ftp URL.`;
            break;
        case validationNamespace + 'PhoneAttribute':
            if (isManagedNull(value)) return null;
            valid = /^(?=.*\d)[0-9+().\-\s]+$/.test(String(scalar));
            fallback = `The ${name} field is not a valid phone number.`;
            break;
        case validationNamespace + 'CompareAttribute': {
            const otherName = String(args[0]);
            const other = findProperty(instance.t, otherName);
            valid = unwrap(invokeGetter(ctx, instance, other)) === scalar;
            fallback = `'${name}' and '${otherName}' do not match.`;
            break;
        }
        default:
            return undefined;
    }
    return valid ? null : explicitMessage(attribute, [name, ...args]) ?? fallback;
}

function wrapAttributeValue(value, typeReference) {
    const typeName = getManagedTypeName(typeReference);
    if (typeName.endsWith('[]') && Array.isArray(value)) {
        return wrap(value.map(item => wrap(item, typeName.slice(0, -2))), typeName);
    }
    return wrap(value, typeName);
}

function constructCustomAttribute(ctx, metadata) {
    const constructor = globalThis.DotNet.Methods[metadata.constructor];
    if (typeof constructor !== 'function') return null;
    const attribute = initializeObject(metadata.typeName);
    constructor(ctx, attribute, ...(metadata.constructorArguments ?? []).map((value, index) =>
        wrapAttributeValue(value, metadata.constructorArgumentTypes[index])));
    const properties = typeHierarchy(metadata.typeName).flatMap(type => type.properties ?? []);
    for (const [name, value] of Object.entries(metadata.namedArguments ?? {})) {
        const property = properties.find(candidate => candidate.name === name);
        if (property && typeof property.setter === 'string') {
            globalThis.DotNet.Methods[property.setter](ctx, attribute, wrapAttributeValue(value, property));
        }
    }
    return attribute;
}

function findMethod(typeName, name, parameterCount) {
    for (const type of typeHierarchy(typeName)) {
        const method = (type.methods ?? []).find(candidate =>
            candidate.name === name && candidate.parameterTypes?.length === parameterCount);
        if (method && typeof globalThis.DotNet.Methods[method.methodName] === 'function') {
            return globalThis.DotNet.Methods[method.methodName];
        }
    }
    return null;
}

function invokeCustomAttribute(ctx, metadata, value, validationContext) {
    const attribute = constructCustomAttribute(ctx, metadata);
    if (attribute === null) return undefined;
    const method = findMethod(attribute.t, 'IsValid', 2);
    return method === null ? undefined : method(ctx, attribute, value, validationContext);
}

export function constructValidationResult(ctx, message, memberNames) {
    const result = initializeObject(validationNamespace + 'ValidationResult');
    globalThis.DotNet.Methods[
        validationNamespace + 'ValidationResult..ctor(System.String, System.Collections.Generic.IEnumerable`1[T])'](
        ctx,
        result,
        wrap(message, 'System.String'),
        wrap(memberNames.map(name => wrap(name, 'System.String')), 'System.String[]'));
    return result;
}

export function addResult(ctx, collection, result) {
    if (isManagedNull(collection)) return;
    const add = globalThis.DotNet.Methods[`${collection.t}.Add(T)`]
        ?? globalThis.DotNet.Methods[collection.t + '.System.Collections.Generic.ICollection`1.Add(T)'];
    if (typeof add !== 'function') {
        throw new Error(`Validation result collection '${collection.t}' does not expose Add(T).`);
    }
    const previousGenericParams = ctx.genericParams;
    try {
        ctx.genericParams = collection.$typeArgs ?? null;
        add(ctx, collection, result);
    } finally {
        ctx.genericParams = previousGenericParams;
    }
}

function setContextMemberName(validationContext, memberName) {
    if (isManagedNull(validationContext)) return;
    const context = unwrap(validationContext);
    context['<MemberName>k__BackingField'] = wrap(memberName, 'System.String');
    context._displayName = wrap(null, 'System.String');
}

export function validateAttributes(
    ctx, attributes, value, property, instance, validationContext, validationResults) {
    let valid = true;
    for (const attribute of attributes) {
        const builtInMessage = validateBuiltIn(ctx, attribute, value, property, instance);
        if (builtInMessage === null) continue;
        if (typeof builtInMessage === 'string') {
            valid = false;
            addResult(ctx, validationResults,
                constructValidationResult(ctx, builtInMessage, property ? [property.name] : []));
            continue;
        }
        const customResult = invokeCustomAttribute(ctx, attribute, value, validationContext);
        if (customResult !== undefined && !isManagedNull(customResult)) {
            valid = false;
            addResult(ctx, validationResults, customResult);
        }
    }
    return valid;
}

export function validateObject(
    ctx, instance, validationContext, validationResults, validateAllProperties) {
    if (isManagedNull(instance)) {
        throwManagedException(ctx, 'System.ArgumentNullException', 'Value cannot be null.');
    }
    const contextInstance = contextState(validationContext).instance;
    if (contextInstance !== instance) {
        throwManagedException(
            ctx,
            'System.ArgumentException',
            'The instance provided must match the ObjectInstance on the ValidationContext.');
    }
    let valid = true;
    for (const type of typeHierarchy(instance.t).reverse()) {
        for (const property of type.properties ?? []) {
            if (property.indexParameterCount !== 0 || typeof property.getter !== 'string') continue;
            const attributes = (property.attributes ?? []).filter(attribute =>
                validateAllProperties || attribute.typeName === validationNamespace + 'RequiredAttribute');
            if (attributes.length === 0) continue;
            setContextMemberName(validationContext, property.name);
            valid = validateAttributes(
                ctx,
                attributes,
                invokeGetter(ctx, instance, property),
                property,
                instance,
                validationContext,
                validationResults) && valid;
        }
    }
    setContextMemberName(validationContext, null);
    if (valid) {
        for (const type of typeHierarchy(instance.t).reverse()) {
            valid = validateAttributes(
                ctx,
                type.attributes ?? [],
                instance,
                null,
                instance,
                validationContext,
                validationResults) && valid;
        }
    }
    if (valid) {
        const implementsValidatable = typeHierarchy(instance.t).some(type =>
            (type.interfaces ?? []).some(reference =>
                getManagedTypeName(reference)
                    === validationNamespace + 'IValidatableObject'));
        const validate = implementsValidatable ? findMethod(instance.t, 'Validate', 1) : null;
        if (validate !== null) {
            const returned = validate(ctx, instance, validationContext);
            const returnedValue = unwrap(returned);
            const results = Array.isArray(returnedValue)
                ? returnedValue
                : tryGetListItems(returned) ?? [];
            for (const result of results) {
                if (isManagedNull(result)) {
                    throw new Error('IValidatableObject.Validate returned a null result.');
                }
                valid = false;
                addResult(ctx, validationResults, result);
            }
        }
    }
    return valid;
}

function contextState(validationContext) {
    const context = unwrap(validationContext);
    return {
        instance: context['<ObjectInstance>k__BackingField'],
        memberName: unwrap(context['<MemberName>k__BackingField'])
    };
}

export function validateProperty(
    ctx, value, validationContext, validationResults) {
    const state = contextState(validationContext);
    const property = findProperty(state.instance?.t, state.memberName);
    if (property === null) {
        throw new Error(`ValidationContext member '${state.memberName}' is not a public property.`);
    }
    return validateAttributes(
        ctx,
        property.attributes ?? [],
        value,
        property,
        state.instance,
        validationContext,
        validationResults);
}

function liveAttributeMetadata(attribute) {
    const value = unwrap(attribute);
    const metadata = { typeName: attribute.t, constructorArguments: [], namedArguments: {} };
    switch (attribute.t) {
        case validationNamespace + 'RequiredAttribute':
            metadata.namedArguments.AllowEmptyStrings = Boolean(unwrap(value._allowEmptyStrings));
            break;
        case validationNamespace + 'StringLengthAttribute':
            metadata.constructorArguments.push(unwrap(value['<MaximumLength>k__BackingField']));
            metadata.namedArguments.MinimumLength = unwrap(value['<MinimumLength>k__BackingField']);
            break;
        case validationNamespace + 'MinLengthAttribute':
        case validationNamespace + 'MaxLengthAttribute':
            metadata.constructorArguments.push(unwrap(value._length));
            break;
        case validationNamespace + 'RangeAttribute':
            metadata.constructorArguments.push(unwrap(value.Minimum), unwrap(value.Maximum));
            break;
        case validationNamespace + 'RegularExpressionAttribute':
            metadata.constructorArguments.push(unwrap(value.Pattern));
            break;
    }
    const errorMessage = unwrap(value.ErrorMessage ?? value._errorMessage);
    if (typeof errorMessage === 'string') metadata.namedArguments.ErrorMessage = errorMessage;
    return metadata;
}

function liveAttributes(attributes) {
    const values = unwrap(attributes);
    if (!Array.isArray(values)) {
        throw new Error('TryValidateValue currently requires an array of validation attributes.');
    }
    return values.map(liveAttributeMetadata);
}

export function validateValue(
    ctx, value, validationContext, validationResults, validationAttributes) {
    return validateAttributes(
        ctx,
        liveAttributes(validationAttributes),
        value,
        null,
        contextState(validationContext).instance,
        validationContext,
        validationResults);
}

globalThis.DotNet.Methods['System.ComponentModel.DataAnnotations.Validator.TryValidateObject(System.Object, System.ComponentModel.DataAnnotations.ValidationContext, System.Collections.Generic.ICollection`1[T], System.Boolean)'] = function (
    ctx, instance, validationContext, validationResults, validateAllProperties) {
    return wrap(validateObject(
        ctx,
        instance,
        validationContext,
        validationResults,
        Boolean(unwrap(validateAllProperties))), 'System.Boolean');
};
