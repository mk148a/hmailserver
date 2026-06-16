namespace HMailServer.Security;

public enum LegacyPasswordEncryptionType : byte
{
    None = 0,
    BlowFish = 1,
    MD5 = 2,
    SHA256 = 3
}
