globalThis.DotNet.Methods['System.Threading.CancellationTokenSource.Dispose()'] = function (ctx, $this) {
    $this.v.$fecsDisposed = true;
    $this.v.$fecsCancellationCallbacks?.clear();
};
