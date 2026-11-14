import assert from "node:assert/strict";
import test from "node:test";

import {
    compileLambda,
    createLambda,
    createParameter
} from "../../JsRuntime/runtime-assets/System.Linq.Expressions.fecs/expression-trees.js";
import { unwrap, wrap } from "../../JsRuntime/msil-type-helpers.js";

test("compiled expressions fail closed outside parameter-rooted property access", () => {
    const parameter = createParameter(
        wrap("Tests.Model", "System.Type"),
        wrap("model", "System.String"));
    const unsupportedBody = wrap({ kind: "add" }, "System.Linq.Expressions.BinaryExpression");
    const lambda = createLambda(
        unsupportedBody,
        wrap([parameter], "System.Linq.Expressions.ParameterExpression[]"),
        { typeName: "System.Func`2", genericParams: ["Tests.Model", "System.Int32"] });
    const compiled = unwrap(compileLambda({}, lambda));

    assert.throws(
        () => compiled(wrap({}, "Tests.Model")),
        /parameter-rooted property access only; found 'add'/);
});
