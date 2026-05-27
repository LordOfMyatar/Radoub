using Radoub.Formats.Bic;
using Radoub.Formats.Utc;

if (args.Length < 1)
{
    Console.WriteLine("Usage: BicInspector <file1> [file2]");
    Console.WriteLine("  Dumps key creature fields; if two files given, diffs them.");
    return 1;
}

var snapshots = args.Select(LoadSnapshot).ToList();

for (int i = 0; i < snapshots.Count; i++)
{
    Console.WriteLine($"=== File {i + 1}: {args[i]} ===");
    PrintSnapshot(snapshots[i]);
    Console.WriteLine();
}

if (snapshots.Count == 2)
{
    Console.WriteLine("=== DIFF (file1 vs file2) ===");
    Diff(snapshots[0], snapshots[1]);
}

return 0;

static Snapshot LoadSnapshot(string path)
{
    var bytes = File.ReadAllBytes(path);
    var ext = Path.GetExtension(path).ToLowerInvariant();

    UtcFile creature;
    bool isBic;
    string detectedType;

    var fileTypeMarker = System.Text.Encoding.ASCII.GetString(bytes, 0, 4).TrimEnd();
    detectedType = fileTypeMarker;

    if (fileTypeMarker == "BIC")
    {
        creature = BicReader.Read(bytes);
        isBic = true;
    }
    else
    {
        creature = UtcReader.Read(bytes);
        isBic = false;
    }

    return new Snapshot(path, bytes.Length, detectedType, isBic, creature);
}

static void PrintSnapshot(Snapshot s)
{
    var c = s.Creature;
    Console.WriteLine($"  Size:           {s.SizeBytes} bytes");
    Console.WriteLine($"  FileType:       '{c.FileType}' (detected: '{s.DetectedType}')");
    Console.WriteLine($"  Version:        {c.FileVersion}");
    Console.WriteLine($"  IsPC:           {c.IsPC}");
    Console.WriteLine($"  TemplateResRef: '{c.TemplateResRef}'");
    Console.WriteLine($"  Tag:            '{c.Tag}'");
    Console.WriteLine($"  FirstName:      '{c.FirstName.GetDefault() ?? ""}'");
    Console.WriteLine($"  LastName:       '{c.LastName.GetDefault() ?? ""}'");
    Console.WriteLine($"  Race:           {c.Race}");
    Console.WriteLine($"  Gender:         {c.Gender}");
    Console.WriteLine($"  Str/Dex/Con/Int/Wis/Cha: {c.Str}/{c.Dex}/{c.Con}/{c.Int}/{c.Wis}/{c.Cha}");
    Console.WriteLine($"  HP (cur/max):   {c.CurrentHitPoints}/{c.MaxHitPoints} (base {c.HitPoints})");
    Console.WriteLine($"  Portrait:       PortraitId={c.PortraitId} '{c.Portrait ?? ""}'");
    Console.WriteLine($"  Total Level:    {c.ClassList.Sum(cl => cl.ClassLevel)}");
    Console.WriteLine($"  Classes:");
    foreach (var cl in c.ClassList)
        Console.WriteLine($"    Class={cl.Class} Level={cl.ClassLevel}");
    Console.WriteLine($"  Feats:          {c.FeatList.Count}");
    Console.WriteLine($"  SkillList:      {c.SkillList.Count} entries, sum={c.SkillList.Sum(b => (int)b)}");
    Console.WriteLine($"  Inventory:      {c.ItemList.Count} items, {c.EquipItemList.Count} equipped");

    if (s.IsBic && c is BicFile bic)
    {
        Console.WriteLine($"  --- BIC-only ---");
        Console.WriteLine($"  Age:            {bic.Age}");
        Console.WriteLine($"  Experience:     {bic.Experience}");
        Console.WriteLine($"  Gold:           {bic.Gold}");
        Console.WriteLine($"  QBList:         {bic.QBList.Count} slots");
        Console.WriteLine($"  ReputationList: {bic.ReputationList.Count} entries");
        Console.WriteLine($"  LvlStatList:    {bic.LvlStatList.Count} entries");
        for (int i = 0; i < bic.LvlStatList.Count; i++)
        {
            var l = bic.LvlStatList[i];
            Console.WriteLine($"    [{i}] Class={l.LvlStatClass} HitDie={l.LvlStatHitDie} Epic={l.EpicLevel} SkillPts={l.SkillPoints}");
        }
    }
}

static void Diff(Snapshot a, Snapshot b)
{
    Cmp("FileType",       $"'{a.Creature.FileType}'", $"'{b.Creature.FileType}'");
    Cmp("IsPC",           a.Creature.IsPC, b.Creature.IsPC);
    Cmp("TotalLevel",     a.Creature.ClassList.Sum(c => c.ClassLevel), b.Creature.ClassList.Sum(c => c.ClassLevel));
    Cmp("FirstName",      a.Creature.FirstName.GetDefault() ?? "", b.Creature.FirstName.GetDefault() ?? "");
    Cmp("LastName",       a.Creature.LastName.GetDefault() ?? "", b.Creature.LastName.GetDefault() ?? "");
    Cmp("Tag",            a.Creature.Tag, b.Creature.Tag);
    Cmp("Race",           a.Creature.Race, b.Creature.Race);
    Cmp("Str",            a.Creature.Str, b.Creature.Str);
    Cmp("Dex",            a.Creature.Dex, b.Creature.Dex);
    Cmp("Con",            a.Creature.Con, b.Creature.Con);
    Cmp("Int",            a.Creature.Int, b.Creature.Int);
    Cmp("Wis",            a.Creature.Wis, b.Creature.Wis);
    Cmp("Cha",            a.Creature.Cha, b.Creature.Cha);
    Cmp("ClassList.Count",a.Creature.ClassList.Count, b.Creature.ClassList.Count);
    Cmp("FeatList.Count", a.Creature.FeatList.Count, b.Creature.FeatList.Count);
    Cmp("SkillSum",       a.Creature.SkillList.Sum(s => (int)s), b.Creature.SkillList.Sum(s => (int)s));
    Cmp("Portrait",       a.Creature.Portrait ?? "", b.Creature.Portrait ?? "");
    Cmp("Inventory",      a.Creature.ItemList.Count, b.Creature.ItemList.Count);
    Cmp("Equipped",       a.Creature.EquipItemList.Count, b.Creature.EquipItemList.Count);

    if (a.Creature is BicFile ab && b.Creature is BicFile bb)
    {
        Console.WriteLine("--- BIC-only ---");
        Cmp("Age",        ab.Age, bb.Age);
        Cmp("Experience", ab.Experience, bb.Experience);
        Cmp("Gold",       ab.Gold, bb.Gold);
        Cmp("QBList",     ab.QBList.Count, bb.QBList.Count);
        Cmp("ReputationList", ab.ReputationList.Count, bb.ReputationList.Count);
        Cmp("LvlStatList",ab.LvlStatList.Count, bb.LvlStatList.Count);
    }
    else if (a.IsBic != b.IsBic)
    {
        Console.WriteLine("(Cross-format diff: BIC-only fields not comparable)");
    }
}

static void Cmp(string label, object? x, object? y)
{
    var match = Equals(x, y) ? "==" : "!=";
    var mark = Equals(x, y) ? " " : "*";
    Console.WriteLine($"  {mark} {label,-20} {x,-30} {match} {y}");
}

record Snapshot(string Path, long SizeBytes, string DetectedType, bool IsBic, UtcFile Creature);
