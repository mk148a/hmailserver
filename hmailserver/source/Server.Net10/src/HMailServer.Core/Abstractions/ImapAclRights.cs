namespace HMailServer.Core.Abstractions;

public static class ImapAclRights
{
    public const long Lookup = 1;
    public const long Read = 2;
    public const long WriteSeen = 4;
    public const long WriteOthers = 8;
    public const long Insert = 16;
    public const long Post = 32;
    public const long CreateMailbox = 64;
    public const long DeleteMailbox = 128;
    public const long WriteDeleted = 256;
    public const long Expunge = 512;
    public const long Administer = 1024;
    public const long All = Lookup | Read | WriteSeen | WriteOthers | Insert | Post | CreateMailbox | DeleteMailbox | WriteDeleted | Expunge | Administer;

    private static readonly (char Name, long Value)[] OrderedRights =
    [
        ('l', Lookup),
        ('r', Read),
        ('s', WriteSeen),
        ('w', WriteOthers),
        ('i', Insert),
        ('p', Post),
        ('k', CreateMailbox),
        ('x', DeleteMailbox),
        ('t', WriteDeleted),
        ('e', Expunge),
        ('a', Administer)
    ];

    public static string Format(long value)
    {
        var builder = new System.Text.StringBuilder(capacity: OrderedRights.Length);
        foreach (var (name, right) in OrderedRights)
        {
            if ((value & right) == right)
            {
                builder.Append(name);
            }
        }

        return builder.ToString();
    }

    public static bool TryParseChange(string value, out ImapAclRightsChange change)
    {
        ArgumentNullException.ThrowIfNull(value);

        var mode = ImapAclRightsChangeMode.Replace;
        var start = 0;
        if (value.Length > 0 && value[0] == '+')
        {
            mode = ImapAclRightsChangeMode.Add;
            start = 1;
        }
        else if (value.Length > 0 && value[0] == '-')
        {
            mode = ImapAclRightsChangeMode.Remove;
            start = 1;
        }

        var rights = 0L;
        for (var index = start; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '+' or '-')
            {
                continue;
            }

            if (!TryGetRight(character, out var right))
            {
                change = new ImapAclRightsChange(ImapAclRightsChangeMode.Replace, 0);
                return false;
            }

            rights |= right;
        }

        change = new ImapAclRightsChange(mode, rights);
        return true;
    }

    private static bool TryGetRight(char character, out long right)
    {
        foreach (var (name, value) in OrderedRights)
        {
            if (character == name)
            {
                right = value;
                return true;
            }
        }

        right = 0;
        return false;
    }
}
