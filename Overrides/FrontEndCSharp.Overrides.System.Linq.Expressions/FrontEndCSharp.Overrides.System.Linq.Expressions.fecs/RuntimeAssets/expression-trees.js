import {
    getManagedTypeArguments,
    getManagedTypeName,
    resolveManagedTypeReference,
    unwrap,
    wrap
} from '../../msil-type-helpers.js';

export function createParameter(type, name) {
    const typeReference = {
        typeName: unwrap(type),
        genericParams: type.$typeArgs
    };
    return wrap({
        kind: 'parameter',
        name: unwrap(name),
        typeName: getManagedTypeName(typeReference),
        typeReference
    }, 'System.Linq.Expressions.ParameterExpression');
}

export function createProperty(expression, accessor) {
    const method = unwrap(accessor);
    const declaringType = method.declaringType;
    const property = globalThis.DotNet?.Types?.[declaringType]?.properties?.find(candidate =>
        candidate.getter === method.metadata.methodName
        || candidate.setter === method.metadata.methodName);
    if (!property) {
        throw new Error(`Property metadata is unavailable for accessor '${method.metadata.methodName}'.`);
    }
    const typeReference = resolveManagedTypeReference(
        property,
        method.declaringTypeArguments);
    return wrap({
        kind: 'member',
        expression,
        member: wrap({
            declaringType,
            declaringTypeArguments: method.declaringTypeArguments,
            metadata: property
        }, 'System.Reflection.PropertyInfo'),
        typeName: getManagedTypeName(typeReference),
        typeReference
    }, 'System.Linq.Expressions.MemberExpression');
}

export function createLambda(body, parameters, delegateType = null) {
    const genericTypeMap = delegateType === null ? null : { TDelegate: delegateType };
    return wrap({
        kind: 'lambda',
        body,
        parameters: unwrap(parameters),
        returnType: unwrap(body).typeReference ?? unwrap(body).typeName
    }, 'System.Linq.Expressions.Expression`1[TDelegate]', genericTypeMap);
}

export function compileLambda(ctx, lambda, delegateType) {
    const lambdaValue = unwrap(lambda);
    const parameters = lambdaValue.parameters ?? [];

    function evaluate(node, argumentsToUse) {
        const value = unwrap(node);
        if (value?.kind === 'parameter') {
            const index = parameters.findIndex(parameter => unwrap(parameter) === value);
            if (index < 0) throw new Error('The expression references an unknown parameter.');
            return argumentsToUse[index];
        }
        if (value?.kind === 'member') {
            const target = evaluate(value.expression, argumentsToUse);
            const member = unwrap(value.member);
            const getterName = member?.metadata?.getter;
            const getter = globalThis.DotNet.Methods[getterName];
            if (typeof getter !== 'function') {
                throw new Error(`The expression member getter '${getterName}' is unavailable.`);
            }
            return getter(ctx, target);
        }
        throw new Error(`Expression compilation supports parameter-rooted property access only; found '${value?.kind ?? 'unknown'}'.`);
    }

    const typeReference = delegateType ?? lambda.$typeArgs?.TDelegate;
    if (typeReference == null) throw new Error('The compiled expression delegate type is unavailable.');
    return wrap(
        (...argumentsToUse) => evaluate(lambdaValue.body, argumentsToUse),
        getManagedTypeName(typeReference),
        getManagedTypeArguments(typeReference));
}

export function getLambdaBody(lambda) {
    return unwrap(lambda).body;
}

export function getLambdaReturnType(lambda) {
    const typeReference = unwrap(lambda).returnType;
    return wrap(
        getManagedTypeName(typeReference),
        'System.Type',
        getManagedTypeArguments(typeReference));
}

export function getMember(memberExpression) {
    return unwrap(memberExpression).member;
}

export function getMemberExpression(memberExpression) {
    return unwrap(memberExpression).expression;
}

export function getMemberNodeType() {
    return wrap(23, 'System.Linq.Expressions.ExpressionType');
}

export function getExpressionNodeType(expression) {
    const kind = unwrap(expression)?.kind;
    if (kind === 'member') return wrap(23, 'System.Linq.Expressions.ExpressionType');
    if (kind === 'parameter') return wrap(38, 'System.Linq.Expressions.ExpressionType');
    if (kind === 'lambda') return wrap(18, 'System.Linq.Expressions.ExpressionType');
    throw new Error(`Expression node type is unavailable for '${kind ?? 'unknown'}'.`);
}

export function getParameterName(parameter) {
    return wrap(unwrap(parameter).name, 'System.String');
}

export function expressionToString(expression) {
    function format(node) {
        const value = unwrap(node);
        if (value?.kind === 'parameter') return value.name ?? '';
        if (value?.kind === 'member') {
            const target = format(value.expression);
            const memberName = unwrap(value.member)?.metadata?.name;
            return target.length === 0 ? memberName : `${target}.${memberName}`;
        }
        throw new Error(`Expression formatting supports property access only; found '${value?.kind ?? 'unknown'}'.`);
    }
    return wrap(format(expression), 'System.String');
}
