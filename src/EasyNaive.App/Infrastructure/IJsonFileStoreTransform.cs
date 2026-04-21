namespace EasyNaive.App.Infrastructure;

internal interface IJsonFileStoreTransform<T>
{
    T AfterLoad(T value);

    T BeforeSave(T value);
}
