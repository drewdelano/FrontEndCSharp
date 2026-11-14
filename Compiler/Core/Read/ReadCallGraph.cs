using Compiler.DTOs;

namespace Compiler.Read
{
    public class ReadCallGraph
    {
        public static void LoadExceptionsThrownFromFrameworkCore(ReaderContext ctx)
            => CallGraphMethodProcessor.LoadExceptionsThrownFromFrameworkCore(ctx);

        public static void ProcessQueuedMethods(
            ReaderContext ctx,
            ref bool hasMissingIntrinsics,
            ref bool foundNewMethods)
            => CallGraphMethodProcessor.ProcessQueuedMethods(
                ctx,
                ref hasMissingIntrinsics,
                ref foundNewMethods);

        public static void FindRuntimeOverrideManagedParameterMembers(
            ReaderContext ctx,
            bool validateResolvedTypes)
            => RuntimeOverrideManagedTypeCoordinator.FindRuntimeOverrideManagedParameterMembers(
                ctx,
                validateResolvedTypes);
    }
}
