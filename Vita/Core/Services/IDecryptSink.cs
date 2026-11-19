namespace Vita.Core.Services;

internal interface IDecryptSink : IDisposable
{
    Stream Open(string entryPath);
}
