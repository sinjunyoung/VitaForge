using Common.Localization;
using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class NoNpDrmDecryptor
{
    public static PfsFileTable ParseFileTable(ISourceAccessor accessor, string titleRelPath)
    {
        string filesDbRel = Combine(titleRelPath, "sce_pfs/files.db");
        string unicvDbRel = Combine(titleRelPath, "sce_pfs/unicv.db");

        if (!accessor.FileExists(filesDbRel))
            throw new FileNotFoundException(Loc.S.ErrFilesDbNotFound, filesDbRel);

        if (!accessor.FileExists(unicvDbRel))
            throw new FileNotFoundException(Loc.S.ErrUnicvDbNotFound, unicvDbRel);

        using var filesDbStream = new MemoryStream(accessor.ReadAllBytes(filesDbRel));
        var flat = PfsFilesDbParser.Parse(filesDbStream, out uint filesSalt);
        using var unicvDbStream = new MemoryStream(accessor.ReadAllBytes(unicvDbRel));
        var unicv = PfsUnicvDbParser.Parse(unicvDbStream, flat.Count);

        return new PfsFileTable { Entries = flat, UnicvEntries = unicv, FilesSalt = filesSalt };
    }

    public static byte[] DecryptEntry(ISourceAccessor accessor, string titleRelPath, byte[] klicensee, PfsFlatEntry entry, PfsUnicvEntry unicvEntry, uint filesSalt)
    {
        string relativePath = entry.RelativePath ?? entry.Name;
        string srcRel = Combine(titleRelPath, relativePath);
        byte[] data = accessor.ReadAllBytes(srcRel);

        if (!entry.Type.IsEncrypted() || unicvEntry.NSectors == 0 || data.Length == 0)
            return data;

        CreateCipher(klicensee, unicvEntry, filesSalt)?.DecryptRange(0, data);

        return data;
    }

    public static void DecryptEntryTo(ISourceAccessor accessor, string titleRelPath, byte[] klicensee, PfsFlatEntry entry, PfsUnicvEntry unicvEntry, uint filesSalt, Stream destination, Action<long>? onProgress = null)
    {
        string relativePath = entry.RelativePath ?? entry.Name;
        string srcRel = Combine(titleRelPath, relativePath);
        long sourceSize = accessor.GetFileSize(srcRel);
        var scaler = new ProgressScaler(sourceSize, entry.Size, onProgress);
        PfsGameDataCipher? cipher = entry.Type.IsEncrypted() && unicvEntry.NSectors > 0 && sourceSize > 0 ? CreateCipher(klicensee, unicvEntry, filesSalt) : null;

        if (cipher is null)
            accessor.CopyTo(srcRel, destination, scaler.Report);
        else
        {
            var decryptStream = new PfsDecryptWriteStream(destination, cipher, (int)unicvEntry.FileSectorSize);

            accessor.CopyTo(srcRel, decryptStream, scaler.Report);
            decryptStream.Finish();
        }

        scaler.Complete();
    }

    private static PfsGameDataCipher? CreateCipher(byte[] klicensee, PfsUnicvEntry unicvEntry, uint filesSalt)
    {
        var f00d = new F00DEmulator();

        if (unicvEntry.HasDbSeed)
            return new PfsGameDataCipher(f00d, klicensee, unicvEntry.DbSeed, (int)unicvEntry.FileSectorSize);

        if (unicvEntry.TableMagic == "SCEIFTBL")
        {
            byte[] tweakEncKey = PfsLegacyKeyDerivation.ComputeTweakEncKey(filesSalt, (uint)unicvEntry.PageNumber);
            return PfsGameDataCipher.FromPrecomputedTweakKey(f00d, klicensee, tweakEncKey, (int)unicvEntry.FileSectorSize);
        }

        return null;
    }

    private static string Combine(string basePath, string relative) => string.IsNullOrEmpty(basePath) ? relative : $"{basePath.TrimEnd('/')}/{PatchShared.NormalizePath(relative)}";
}