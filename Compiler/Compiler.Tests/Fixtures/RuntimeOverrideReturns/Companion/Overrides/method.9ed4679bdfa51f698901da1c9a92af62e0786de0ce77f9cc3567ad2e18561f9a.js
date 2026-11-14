import '../runtime-assets/RuntimeOverrideReturns.Contracts.fecs/fixture.js';

globalThis.DotNet.Methods['RuntimeOverrideReturns.ReturnFactory.CreateContract()'] = function () {
    return { t: 'RuntimeOverrideReturns.ReturnContractImplementation', v: {} };
};
