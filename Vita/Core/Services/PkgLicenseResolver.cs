using Common.Localization;
using System.Text;
using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PkgLicenseResolver
{
    public static byte[] ResolveKlicensee(string license, string expectedContentId)
    {
        var (contentId, klicensee, fromFile) = ReadLicense(license);

        if (!contentId.Equals(expectedContentId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(MismatchMessage(fromFile, contentId, expectedContentId));

        return klicensee;
    }

    public static byte[] ResolveKlicenseeForPatch(string license, string patchContentId)
    {
        var (contentId, klicensee, fromFile) = ReadLicense(license);

        if (!HasSameTitleId(contentId, patchContentId))
            throw new InvalidDataException(MismatchMessage(fromFile, contentId, patchContentId));

        return klicensee;
    }

    public static byte[] BuildWorkBin(string contentId, byte[] klicensee)
    {
        var buf = new byte[WorkBinLicense.WriteSize];
        var contentIdBytes = Encoding.ASCII.GetBytes(contentId);

        WorkBinLicense.FixedHeader.CopyTo(buf, 0);
        contentIdBytes.CopyTo(buf, WorkBinLicense.ContentIdOffset);
        klicensee.CopyTo(buf, WorkBinLicense.KlicenseeOffset);

        return buf;
    }

    private static (string ContentId, byte[] Klicensee, bool FromFile) ReadLicense(string license)
    {
        if (File.Exists(license))
        {
            var parsed = WorkBinReader.Read(license);

            return (parsed.ContentId, parsed.Klicensee, true);
        }

        byte[] rif = ZrifDecoder.Decode(license);
        string contentId = Encoding.ASCII.GetString(rif, WorkBinLicense.ContentIdOffset, WorkBinLicense.ContentIdSize).TrimEnd('\0');

        return (contentId, rif.AsSpan(WorkBinLicense.KlicenseeOffset, WorkBinLicense.KlicenseeSize).ToArray(), false);
    }

    private static string MismatchMessage(bool fromFile, string licenseContentId, string expectedContentId)
        => fromFile ? Loc.S.ErrVitaLicenseContentIdMismatch(licenseContentId, expectedContentId) : Loc.S.ErrVitaZrifContentIdMismatch(licenseContentId, expectedContentId);

    private static bool HasSameTitleId(string a, string b)
        => WorkBinReader.TryGetTitleIdFromContentId(a, out string titleA) && WorkBinReader.TryGetTitleIdFromContentId(b, out string titleB) && titleA.Equals(titleB, StringComparison.OrdinalIgnoreCase);
}