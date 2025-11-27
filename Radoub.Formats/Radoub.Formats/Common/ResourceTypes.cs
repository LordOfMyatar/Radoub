namespace Radoub.Formats.Common;

/// <summary>
/// Aurora Engine resource type identifiers.
/// Reference: neverwinter.nim restype.nim
/// </summary>
public static class ResourceTypes
{
    // Invalid/placeholder
    public const ushort Invalid = 0xFFFF;

    // Core resource types (0-9)
    public const ushort Res = 0;
    public const ushort Bmp = 1;
    public const ushort Mve = 2;
    public const ushort Tga = 3;
    public const ushort Wav = 4;
    public const ushort Wfx = 5;
    public const ushort Plt = 6;
    public const ushort Ini = 7;
    public const ushort Mp3 = 8;
    public const ushort Mpg = 9;

    // Text/data types (2000-2099)
    public const ushort Txt = 2000;
    public const ushort Plh = 2001;
    public const ushort Tex = 2002;
    public const ushort Mdl = 2002;  // Model files also use 2002
    public const ushort Thg = 2003;
    public const ushort Fnt = 2005;
    public const ushort Lua = 2007;
    public const ushort Slt = 2008;
    public const ushort Nss = 2009;  // NWScript source
    public const ushort Ncs = 2010;  // NWScript compiled
    public const ushort Mod = 2011;  // Module
    public const ushort Are = 2012;  // Area
    public const ushort Set = 2013;  // Tileset
    public const ushort Ifo = 2014;  // Module info
    public const ushort Bic = 2015;  // Character
    public const ushort Wok = 2016;  // Walkmesh
    public const ushort TwoDA = 2017; // 2DA table
    public const ushort Tlk = 2018;  // Talk table
    public const ushort Txi = 2022;  // Texture info
    public const ushort Git = 2023;  // Game instance
    public const ushort Bti = 2024;
    public const ushort Uti = 2025;  // Item template
    public const ushort Btc = 2026;
    public const ushort Utc = 2027;  // Creature template
    public const ushort Dlg = 2029;  // Dialog
    public const ushort Itp = 2030;  // Item palette
    public const ushort Btt = 2031;
    public const ushort Utt = 2032;  // Trigger template
    public const ushort Dds = 2033;  // DirectDraw Surface
    public const ushort Bts = 2034;
    public const ushort Uts = 2035;  // Sound template
    public const ushort Ltr = 2036;  // Letter combo
    public const ushort Gff = 2037;  // Generic GFF
    public const ushort Fac = 2038;  // Faction
    public const ushort Bte = 2039;
    public const ushort Ute = 2040;  // Encounter template
    public const ushort Btd = 2041;
    public const ushort Utd = 2042;  // Door template
    public const ushort Btp = 2043;
    public const ushort Utp = 2044;  // Placeable template
    public const ushort Dft = 2045;  // Default values
    public const ushort Gic = 2046;  // Game instance comments
    public const ushort Gui = 2047;  // GUI layout
    public const ushort Css = 2048;
    public const ushort Ccs = 2049;
    public const ushort Btm = 2050;
    public const ushort Utm = 2051;  // Merchant template
    public const ushort Dwk = 2052;  // Door walkmesh
    public const ushort Pwk = 2053;  // Placeable walkmesh
    public const ushort Btg = 2054;
    public const ushort Utg = 2055;
    public const ushort Jrl = 2056;  // Journal
    public const ushort Sav = 2057;  // Save game
    public const ushort Utw = 2058;  // Waypoint template
    public const ushort FourPc = 2059; // 4PC
    public const ushort Ssf = 2060;  // Sound set
    public const ushort Hak = 2061;  // Hak pak
    public const ushort Nwm = 2062;
    public const ushort Bik = 2063;  // Bink video
    public const ushort Ndb = 2064;  // Script debug
    public const ushort Ptm = 2065;
    public const ushort Ptt = 2066;
    public const ushort Bak = 2067;
    public const ushort Osc = 2068;
    public const ushort Usc = 2069;
    public const ushort Trn = 2070;
    public const ushort Utr = 2071;
    public const ushort Uen = 2072;
    public const ushort Ult = 2073;
    public const ushort Sef = 2074;
    public const ushort Pfx = 2075;
    public const ushort Cam = 2076;
    public const ushort Lfx = 2077;
    public const ushort Bfx = 2078;
    public const ushort Upe = 2079;
    public const ushort Ros = 2080;
    public const ushort Rst = 2081;
    public const ushort Ifx = 2082;
    public const ushort Pfb = 2083;
    public const ushort Zip = 2084;
    public const ushort Wmp = 2085;
    public const ushort Bbx = 2086;
    public const ushort Tfx = 2087;
    public const ushort Wlk = 2088;
    public const ushort Xml = 2089;
    public const ushort Scc = 2090;
    public const ushort Ptx = 2091;
    public const ushort Ltx = 2092;
    public const ushort Trx = 2093;

    // NWN:EE specific (3000+)
    public const ushort Mdb = 4000;
    public const ushort Mda = 4001;
    public const ushort Spt = 4002;
    public const ushort Gr2 = 4003;
    public const ushort Fxa = 4004;
    public const ushort Fxe = 4005;
    public const ushort Jpg = 4007;
    public const ushort Pwc = 4008;

    // Archive types (9000+)
    public const ushort Ids = 9996;
    public const ushort Erf = 9997;
    public const ushort Bif = 9998;
    public const ushort Key = 9999;

    /// <summary>
    /// Get the file extension for a resource type.
    /// </summary>
    public static string GetExtension(ushort resourceType)
    {
        return resourceType switch
        {
            Res => ".res",
            Bmp => ".bmp",
            Mve => ".mve",
            Tga => ".tga",
            Wav => ".wav",
            Plt => ".plt",
            Ini => ".ini",
            Mp3 => ".mp3",
            Mpg => ".mpg",
            Txt => ".txt",
            Mdl => ".mdl",
            Nss => ".nss",
            Ncs => ".ncs",
            Mod => ".mod",
            Are => ".are",
            Set => ".set",
            Ifo => ".ifo",
            Bic => ".bic",
            Wok => ".wok",
            TwoDA => ".2da",
            Tlk => ".tlk",
            Txi => ".txi",
            Git => ".git",
            Uti => ".uti",
            Utc => ".utc",
            Dlg => ".dlg",
            Itp => ".itp",
            Utt => ".utt",
            Dds => ".dds",
            Uts => ".uts",
            Ltr => ".ltr",
            Gff => ".gff",
            Fac => ".fac",
            Ute => ".ute",
            Utd => ".utd",
            Utp => ".utp",
            Dft => ".dft",
            Gic => ".gic",
            Gui => ".gui",
            Utm => ".utm",
            Dwk => ".dwk",
            Pwk => ".pwk",
            Jrl => ".jrl",
            Sav => ".sav",
            Utw => ".utw",
            Ssf => ".ssf",
            Hak => ".hak",
            Bik => ".bik",
            Ndb => ".ndb",
            Erf => ".erf",
            Bif => ".bif",
            Key => ".key",
            Jpg => ".jpg",
            _ => $".{resourceType}"
        };
    }

    /// <summary>
    /// Get the resource type from a file extension.
    /// </summary>
    public static ushort FromExtension(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "res" => Res,
            "bmp" => Bmp,
            "mve" => Mve,
            "tga" => Tga,
            "wav" => Wav,
            "plt" => Plt,
            "ini" => Ini,
            "mp3" => Mp3,
            "mpg" => Mpg,
            "txt" => Txt,
            "mdl" => Mdl,
            "nss" => Nss,
            "ncs" => Ncs,
            "mod" => Mod,
            "are" => Are,
            "set" => Set,
            "ifo" => Ifo,
            "bic" => Bic,
            "wok" => Wok,
            "2da" => TwoDA,
            "tlk" => Tlk,
            "txi" => Txi,
            "git" => Git,
            "uti" => Uti,
            "utc" => Utc,
            "dlg" => Dlg,
            "itp" => Itp,
            "utt" => Utt,
            "dds" => Dds,
            "uts" => Uts,
            "ltr" => Ltr,
            "gff" => Gff,
            "fac" => Fac,
            "ute" => Ute,
            "utd" => Utd,
            "utp" => Utp,
            "dft" => Dft,
            "gic" => Gic,
            "gui" => Gui,
            "utm" => Utm,
            "dwk" => Dwk,
            "pwk" => Pwk,
            "jrl" => Jrl,
            "sav" => Sav,
            "utw" => Utw,
            "ssf" => Ssf,
            "hak" => Hak,
            "bik" => Bik,
            "ndb" => Ndb,
            "erf" => Erf,
            "bif" => Bif,
            "key" => Key,
            "jpg" => Jpg,
            _ => Invalid
        };
    }
}
