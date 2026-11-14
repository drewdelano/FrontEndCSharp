namespace PossibleTypes;

using System.Threading.Tasks;

internal class Speaker
{
    public virtual string Speak() => "speaker";
    public virtual string ParameterOnly() => "speaker";
    public virtual string FieldOnly() => "speaker";
    public virtual string ArrayOnly() => "speaker";
    public virtual string ClosureOnly() => "speaker";
    public virtual string AsyncOnly() => "speaker";
    public virtual string ExceptionOnly() => "speaker";
    public virtual string DelegateFactoryOnly() => "speaker";

    public string NullOnly() => "speaker";
}

internal sealed class Dog : Speaker
{
    public override string Speak() => "dog";
    public override string ParameterOnly() => "dog";
    public override string FieldOnly() => "dog";
    public override string ArrayOnly() => "dog";
    public override string ClosureOnly() => "dog";
    public override string AsyncOnly() => "dog";
    public override string ExceptionOnly() => "dog";
    public override string DelegateFactoryOnly() => "dog";
}

internal sealed class Cat : Speaker
{
    public override string Speak() => "cat";
    public override string ParameterOnly() => "cat";
    public override string FieldOnly() => "cat";
    public override string ArrayOnly() => "cat";
    public override string ClosureOnly() => "cat";
    public override string AsyncOnly() => "cat";
    public override string ExceptionOnly() => "cat";
}

internal sealed class Fox : Speaker
{
    public override string Speak() => "fox";
    public override string ParameterOnly() => "fox";
    public override string FieldOnly() => "fox";
    public override string ArrayOnly() => "fox";
    public override string ClosureOnly() => "fox";
    public override string AsyncOnly() => "fox";
    public override string ExceptionOnly() => "fox";
    public override string DelegateFactoryOnly() => "fox";
}

internal interface IWorker
{
    string Work();
}

internal sealed class WorkingDog : IWorker
{
    public string Work() => "dog";
}

internal sealed class WorkingFox : IWorker
{
    public string Work() => "fox";
}

internal class AllocationSpeaker
{
    public virtual string FieldOnly() => "speaker";
    public virtual string ArrayOnly() => "speaker";
    public virtual string MergedField() => "speaker";
    public virtual string MergedArray() => "speaker";
}

internal sealed class AllocationDog : AllocationSpeaker
{
    public override string FieldOnly() => "dog";
    public override string ArrayOnly() => "dog";
    public override string MergedField() => "dog";
    public override string MergedArray() => "dog";
}

internal sealed class AllocationFox : AllocationSpeaker
{
    public override string FieldOnly() => "fox";
    public override string ArrayOnly() => "fox";
    public override string MergedField() => "fox";
    public override string MergedArray() => "fox";
}

internal sealed class AllocationSpeakerSlot
{
    public AllocationSpeaker Value = null!;
}

internal sealed class ReflectedModel
{
    public string Name { get; set; } = "value";
}

internal sealed class RuntimeReflectedModel
{
    public int Count { get; set; }
}

internal sealed class RuntimeInspectedOnlyModel
{
    public bool Enabled { get; set; }
}

internal sealed class ClosureReflectedModel
{
    public string Value { get; set; } = "closure";
}

internal sealed class AsyncReflectedModel
{
    public string Value { get; set; } = "async";
}

internal sealed class HandlerReflectedModel
{
    public string Value { get; set; } = "handler";
}

internal sealed class ReadReflectedModel
{
    public string Value { get; set; } = "read";
}

internal sealed class WriteReflectedModel
{
    public string Value { get; set; } = "write";
}

internal sealed class InvokedReflectedModel
{
    public string Run() => "invoked";
}

internal sealed class UninvokedReflectedModel
{
    public string Run() => "uninvoked";
}

internal sealed class UnrelatedTypeToken
{
}

internal static class Program
{
    private static readonly Speaker StoredSpeaker = new Dog();
    private static Speaker CreateSpeaker() => new Dog();

    private static Speaker ChooseSpeaker(bool chooseDog)
        => chooseDog ? new Dog() : new Cat();

    private static Speaker PassThrough(Speaker speaker) => speaker;
    private static AllocationSpeakerSlot PassThroughSlot(AllocationSpeakerSlot slot) => slot;
    private static AllocationSpeaker[] PassThroughArray(AllocationSpeaker[] speakers) => speakers;
    private static AllocationSpeakerSlot ChooseSlot(
        bool chooseDog,
        AllocationSpeakerSlot dog,
        AllocationSpeakerSlot fox) => chooseDog ? dog : fox;
    private static AllocationSpeaker[] ChooseArray(
        bool chooseDog,
        AllocationSpeaker[] dog,
        AllocationSpeaker[] fox) => chooseDog ? dog : fox;
    private static IWorker CreateWorker() => new WorkingDog();
    private static T CreateFromDelegate<T>(System.Func<T> factory) => factory();
    private static readonly System.Type StoredType = typeof(ReflectedModel);
    private static System.Type PassThroughType(System.Type type) => type;

    private static void UseClosure(Speaker speaker)
    {
        System.Action action = () => _ = speaker.ClosureOnly();
        action();
    }

    private static async Task UseAsync(Speaker speaker)
    {
        await Task.Yield();
        _ = speaker.AsyncOnly();
    }

    private static void ReflectFromClosure(System.Type reflectedType)
    {
        System.Action action = () => _ = reflectedType.GetProperties();
        action();
    }

    private static async Task ReflectAfterAwait(System.Type reflectedType)
    {
        await Task.Yield();
        _ = reflectedType.GetProperties();
    }

    private static void ReflectFromHandler(System.Type reflectedType)
    {
        try
        {
            throw new System.InvalidOperationException();
        }
        catch (System.InvalidOperationException)
        {
            _ = reflectedType.GetProperties();
        }
    }

    private static bool ObserveRuntimeException(System.Exception exception)
    {
        _ = exception.GetType().GetProperties();
        return false;
    }

    private static void DivideByZeroOnly()
    {
    }

    private static void InvalidCastOnly()
    {
    }

    private static void UseRuntimeExceptionFlow(int divisor)
    {
        try
        {
            _ = 1 / divisor;
        }
        catch (System.Exception exception) when (ObserveRuntimeException(exception))
        {
        }
        catch (System.DivideByZeroException)
        {
            DivideByZeroOnly();
            Speaker speaker = new Dog();
            _ = speaker.ExceptionOnly();
        }
        catch (System.InvalidCastException)
        {
            InvalidCastOnly();
            Speaker speaker = new Fox();
            _ = speaker.ExceptionOnly();
        }
    }

    public static void Main()
    {
        _ = new Cat();
        _ = new Fox();
        _ = CreateSpeaker().Speak();
        _ = ChooseSpeaker(true).Speak();
        _ = PassThrough(CreateSpeaker()).ParameterOnly();
        _ = StoredSpeaker.FieldOnly();
        Speaker[] speakers = [new Dog()];
        _ = speakers[0].ArrayOnly();
        var dogSlot = new AllocationSpeakerSlot { Value = new AllocationDog() };
        var foxSlot = new AllocationSpeakerSlot { Value = new AllocationFox() };
        _ = PassThroughSlot(dogSlot).Value.FieldOnly();
        _ = ChooseSlot(true, dogSlot, foxSlot).Value.MergedField();
        _ = foxSlot.Value;
        AllocationSpeaker[] dogArray = [new AllocationDog()];
        AllocationSpeaker[] foxArray = [new AllocationFox()];
        _ = PassThroughArray(dogArray)[0].ArrayOnly();
        _ = ChooseArray(true, dogArray, foxArray)[0].MergedArray();
        _ = foxArray.Length;
        _ = new WorkingFox();
        _ = CreateWorker().Work();
        _ = CreateFromDelegate(() => new Dog()).DelegateFactoryOnly();
        UseClosure(CreateSpeaker());
        UseAsync(CreateSpeaker()).GetAwaiter().GetResult();
        _ = typeof(UnrelatedTypeToken).ToString();
        System.Type[] reflectedTypes = [StoredType];
        _ = PassThroughType(reflectedTypes[0]).GetProperties();
        object runtimeReflected = new RuntimeReflectedModel();
        _ = runtimeReflected.GetType().GetProperties();
        object runtimeInspectedOnly = new RuntimeInspectedOnlyModel();
        _ = runtimeInspectedOnly.GetType().ToString();
        ReflectFromClosure(typeof(ClosureReflectedModel));
        ReflectAfterAwait(typeof(AsyncReflectedModel)).GetAwaiter().GetResult();
        ReflectFromHandler(typeof(HandlerReflectedModel));

        var readModel = new ReadReflectedModel();
        foreach (var property in typeof(ReadReflectedModel).GetProperties())
        {
            _ = property.GetValue(readModel);
        }

        var writeModel = new WriteReflectedModel();
        foreach (var property in typeof(WriteReflectedModel).GetProperties())
        {
            property.SetValue(writeModel, "changed");
        }

        var invokedModel = new InvokedReflectedModel();
        foreach (var method in typeof(InvokedReflectedModel).GetMethods())
        {
            _ = method.Invoke(invokedModel, null);
        }

        _ = new UninvokedReflectedModel();
        _ = new System.ArgumentException();
        try
        {
            throw new System.InvalidOperationException();
        }
        catch (System.Exception exception)
        {
            _ = exception.GetType().GetProperties();
        }
        UseRuntimeExceptionFlow(0);
        try
        {
            Speaker nullSpeaker = null!;
            _ = nullSpeaker.NullOnly();
        }
        catch (System.NullReferenceException)
        {
        }
    }
}
