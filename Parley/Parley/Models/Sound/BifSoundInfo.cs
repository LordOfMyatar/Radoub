namespace DialogEditor.Models.Sound
{
    /// <summary>
    /// Sound info from a BIF file (requires KEY to map ResRef to BIF location).
    /// </summary>
    public class BifSoundInfo
    {
        public string ResRef { get; set; } = "";
        public string BifPath { get; set; } = "";
        public int VariableTableIndex { get; set; }
        public uint FileSize { get; set; }
    }
}
